using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BizFlow.Infrastructure.AI;

public sealed class AiEmbeddingService : IAiEmbeddingService
{
    private const int VectorDimensions = 384;
    private readonly HttpClient? _httpClient;
    private readonly string? _apiKey;
    private readonly string _baseUrl;
    private readonly string _embeddingModel;
    private readonly ILogger<AiEmbeddingService>? _logger;

    public AiEmbeddingService(
        IConfiguration configuration,
        HttpClient? httpClient = null,
        ILogger<AiEmbeddingService>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["OpenAI:ApiKey"] ?? configuration["OPENAI_API_KEY"];
        _baseUrl = (configuration["OpenAI:BaseUrl"] ?? configuration["OPENAI_BASE_URL"] ?? "https://api.openai.com/v1").TrimEnd('/');
        _embeddingModel = configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new float[VectorDimensions];

        // 1. Try OpenAI embedding if configured and HTTP client available
        if (!string.IsNullOrWhiteSpace(_apiKey) && _httpClient != null)
        {
            try
            {
                var payload = new
                {
                    model = _embeddingModel,
                    input = text.Length > 2000 ? text[..2000] : text
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/embeddings");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(10));

                var response = await _httpClient.SendAsync(request, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(cts.Token);
                    using var doc = JsonDocument.Parse(responseJson);
                    var data = doc.RootElement.GetProperty("data");
                    if (data.GetArrayLength() > 0)
                    {
                        var embeddingArr = data[0].GetProperty("embedding");
                        var result = new float[embeddingArr.GetArrayLength()];
                        var idx = 0;
                        foreach (var val in embeddingArr.EnumerateArray())
                        {
                            result[idx++] = (float)val.GetDouble();
                        }
                        return NormalizeVector(result);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "OpenAI embedding call failed, using deterministic local embedding vectorizer.");
            }
        }

        // 2. High-Fidelity Local Semantic Vectorizer (N-gram + Token Hashing + Unit Normalization)
        return GenerateLocalSemanticEmbedding(text);
    }

    public static float[] GenerateLocalSemanticEmbedding(string text)
    {
        var vector = new float[VectorDimensions];
        if (string.IsNullOrWhiteSpace(text))
            return vector;

        var normalized = text.ToLowerInvariant().Trim();
        var words = normalized.Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '-', '_', '(', ')', '[', ']', '{', '}' }, StringSplitOptions.RemoveEmptyEntries);

        // Word unigrams and bigrams
        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i];
            var hash1 = Math.Abs(GetFnv1aHashCode(word)) % VectorDimensions;
            vector[hash1] += 2.0f; // Higher weight for whole words

            if (i < words.Length - 1)
            {
                var bigram = $"{word}_{words[i + 1]}";
                var hashBi = Math.Abs(GetFnv1aHashCode(bigram)) % VectorDimensions;
                vector[hashBi] += 1.5f;
            }

            // Character trigrams for subword morphology / Vietnamese syllables
            if (word.Length >= 3)
            {
                for (int c = 0; c <= word.Length - 3; c++)
                {
                    var trigram = word.Substring(c, 3);
                    var hashTri = Math.Abs(GetFnv1aHashCode(trigram)) % VectorDimensions;
                    vector[hashTri] += 0.5f;
                }
            }
        }

        return NormalizeVector(vector);
    }

    private static float[] NormalizeVector(float[] vector)
    {
        double sumSquares = 0.0;
        for (int i = 0; i < vector.Length; i++)
        {
            sumSquares += vector[i] * vector[i];
        }

        if (sumSquares <= 0.0)
            return vector;

        var norm = (float)Math.Sqrt(sumSquares);
        var normalized = new float[vector.Length];
        for (int i = 0; i < vector.Length; i++)
        {
            normalized[i] = vector[i] / norm;
        }

        return normalized;
    }

    private static int GetFnv1aHashCode(string str)
    {
        unchecked
        {
            int hash = (int)2166136261;
            for (int i = 0; i < str.Length; i++)
            {
                hash = (hash ^ str[i]) * 16777619;
            }
            return hash;
        }
    }
}

