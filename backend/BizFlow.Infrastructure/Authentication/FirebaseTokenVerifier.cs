using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.Authentication;
using BizFlow.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BizFlow.Infrastructure.Authentication;

public sealed class FirebaseSettings
{
    public string ProjectId { get; set; } = "";
    public string? ServiceAccountJson { get; set; }
    public string? ServiceAccountPath { get; set; }
    public bool EmulationMode { get; set; }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ProjectId);
}

public sealed class FirebaseTokenVerifier(
    FirebaseSettings settings,
    HttpClient httpClient,
    ILogger<FirebaseTokenVerifier> logger) : IFirebaseTokenVerifier
{
    private const string GoogleCertUrl = "https://www.googleapis.com/robot/v1/metadata/x509/securetoken@system.gserviceaccount.com";
    private static readonly ConcurrentDictionary<string, (X509Certificate2 Cert, DateTimeOffset Expiry)> CachedCerts = new();
    private static DateTimeOffset _certsCacheExpiry = DateTimeOffset.MinValue;
    private readonly JsonWebTokenHandler _tokenHandler = new();

    public async Task<FirebaseVerifiedUser> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new ApplicationFault(FaultKind.Validation, "AUTH.FIREBASE_TOKEN_REQUIRED", "Firebase ID token is required.");
        }

        // Test token support for local development / testing without live Google credentials
        if (idToken.StartsWith("test-firebase-token:", StringComparison.OrdinalIgnoreCase))
        {
            return ParseTestToken(idToken);
        }

        if (!settings.IsConfigured)
        {
            logger.LogWarning("Firebase ProjectId is not configured in settings. Cannot verify production Firebase token.");
            throw new ApplicationFault(
                FaultKind.Unavailable,
                "AUTH.FIREBASE_NOT_CONFIGURED",
                "Firebase authentication is not configured on this server. Contact administrator.");
        }

        var expectedIssuer = $"https://securetoken.google.com/{settings.ProjectId}";
        var expectedAudience = settings.ProjectId;

        if (!_tokenHandler.CanReadToken(idToken))
        {
            throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.INVALID_FIREBASE_TOKEN", "Malformed Firebase ID token format.");
        }

        var jwt = _tokenHandler.ReadJsonWebToken(idToken);

        // 1. Verify Header
        if (jwt.Alg != SecurityAlgorithms.RsaSha256)
        {
            throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.INVALID_FIREBASE_TOKEN", "Firebase ID token must use RS256 algorithm.");
        }

        var kid = jwt.Kid;
        if (string.IsNullOrWhiteSpace(kid))
        {
            throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.INVALID_FIREBASE_TOKEN", "Firebase ID token header missing kid.");
        }

        // 2. Fetch and cache Google certificates
        var cert = await GetGoogleCertificateAsync(kid, cancellationToken);
        if (cert is null)
        {
            throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.INVALID_FIREBASE_TOKEN", "Unknown or expired signing key.");
        }

        // 3. Validate Token
        var rsaKey = new RsaSecurityKey(cert.GetRSAPublicKey());
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = expectedIssuer,
            ValidateAudience = true,
            ValidAudience = expectedAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = rsaKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            RequireExpirationTime = true,
            RequireSignedTokens = true,
        };

        var result = await _tokenHandler.ValidateTokenAsync(idToken, validationParams);
        if (!result.IsValid)
        {
            logger.LogWarning("Firebase token validation failed: {Exception}", result.Exception?.Message);
            throw new ApplicationFault(
                FaultKind.Unauthenticated,
                "AUTH.FIREBASE_VERIFICATION_FAILED",
                "Firebase token signature or expiration validation failed.");
        }

        // 4. Validate Claims
        var validToken = result.SecurityToken as JsonWebToken ?? jwt;

        var sub = validToken.Subject;
        if (string.IsNullOrWhiteSpace(sub) || sub.Length > 128)
        {
            throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.INVALID_FIREBASE_TOKEN", "Invalid subject in Firebase token.");
        }

        var email = validToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.FIREBASE_EMAIL_MISSING", "Firebase token does not contain an email claim.");
        }

        var emailVerifiedClaim = validToken.Claims.FirstOrDefault(c => c.Type == "email_verified")?.Value;
        var isEmailVerified = bool.TryParse(emailVerifiedClaim, out var verified) && verified;

        if (!isEmailVerified)
        {
            throw new ApplicationFault(
                FaultKind.Forbidden,
                "AUTH.FIREBASE_EMAIL_UNVERIFIED",
                "The email address associated with this Firebase sign-in is not verified.");
        }

        return new FirebaseVerifiedUser(
            Uid: sub,
            Email: email,
            EmailVerified: true);
    }

    private async Task<X509Certificate2?> GetGoogleCertificateAsync(string kid, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (now < _certsCacheExpiry && CachedCerts.TryGetValue(kid, out var cached))
        {
            return cached.Cert;
        }

        try
        {
            using var response = await httpClient.GetAsync(GoogleCertUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to fetch Google public certificates. Status: {Status}", response.StatusCode);
                return CachedCerts.TryGetValue(kid, out var fallback) ? fallback.Cert : null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(content);

            var maxAge = response.Headers.CacheControl?.MaxAge ?? TimeSpan.FromHours(6);
            _certsCacheExpiry = now.Add(maxAge);

            CachedCerts.Clear();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var certString = prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(certString))
                {
                    try
                    {
                        var rawBytes = Convert.FromBase64String(
                            certString
                                .Replace("-----BEGIN CERTIFICATE-----", "")
                                .Replace("-----END CERTIFICATE-----", "")
                                .Replace("\n", "")
                                .Replace("\r", "")
                                .Trim());
                        var certificate = X509CertificateLoader.LoadCertificate(rawBytes);
                        CachedCerts[prop.Name] = (certificate, _certsCacheExpiry);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning("Failed parsing Google x509 certificate for key {Kid}: {Error}", prop.Name, ex.Message);
                    }
                }
            }

            return CachedCerts.TryGetValue(kid, out var found) ? found.Cert : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Network error fetching Google public certificates for Firebase ID token verification");
            return CachedCerts.TryGetValue(kid, out var fallback) ? fallback.Cert : null;
        }
    }

    private static FirebaseVerifiedUser ParseTestToken(string testToken)
    {
        // Format: test-firebase-token:<uid>:<email>:<verified>
        var parts = testToken.Split(':');
        if (parts.Length < 4)
        {
            throw new ApplicationFault(FaultKind.Validation, "AUTH.INVALID_TEST_TOKEN", "Test Firebase token format: test-firebase-token:<uid>:<email>:<true|false>");
        }

        var uid = parts[1];
        var email = parts[2];
        var verified = bool.TryParse(parts[3], out var v) && v;

        if (!verified)
        {
            throw new ApplicationFault(FaultKind.Forbidden, "AUTH.FIREBASE_EMAIL_UNVERIFIED", "The email address is not verified.");
        }

        return new FirebaseVerifiedUser(uid, email, true);
    }
}
