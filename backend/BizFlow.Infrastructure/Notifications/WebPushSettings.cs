using System.Security.Cryptography;

namespace BizFlow.Infrastructure.Notifications;

public sealed class WebPushSettings
{
    public bool Enabled{get;init;}
    public string VapidPublicKey{get;init;}="";
    public string VapidPrivateKey{get;init;}="";
    public string Subject{get;init;}="";
    public string[] AllowedHosts{get;init;}=[];
    public bool IsValid
    {
        get
        {
            try
            {
                if(!Uri.TryCreate(Subject,UriKind.Absolute,out var subject) || subject.Scheme is not ("mailto" or "https") || Subject.Any(char.IsControl))return false;
                var secret=Decode(VapidPrivateKey);var advertised=Decode(VapidPublicKey);
                if(secret.Length!=32 || advertised.Length!=65 || advertised[0]!=4)return false;
                using var key=ECDsa.Create(new ECParameters{Curve=ECCurve.NamedCurves.nistP256,D=secret});
                var point=key.ExportParameters(false).Q;
                return advertised.AsSpan(1,32).SequenceEqual(point.X) && advertised.AsSpan(33,32).SequenceEqual(point.Y);
            }
            catch(Exception error) when(error is ArgumentException or FormatException or CryptographicException){return false;}
        }
    }
    public bool AllowsEndpoint(string address)
    {
        if(!Uri.TryCreate(address,UriKind.Absolute,out var uri) || uri.Scheme!="https" || !uri.IsDefaultPort || uri.UserInfo.Length>0 || uri.Fragment.Length>0 ||
            System.Net.IPAddress.TryParse(uri.Host,out _))return false;
        var host=uri.IdnHost;
        return host is "fcm.googleapis.com" or "updates.push.services.mozilla.com" or "web.push.apple.com" ||
            host.EndsWith(".notify.windows.com",StringComparison.OrdinalIgnoreCase) ||
            AllowedHosts.Any(allowed=>host.Equals(allowed,StringComparison.OrdinalIgnoreCase));
    }
    private static byte[] Decode(string value)=>Convert.FromBase64String(value.Replace('-','+').Replace('_','/')+new string('=',(4-value.Length%4)%4));
}
