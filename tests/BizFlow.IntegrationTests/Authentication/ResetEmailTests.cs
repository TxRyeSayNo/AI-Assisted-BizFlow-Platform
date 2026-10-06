using System.Net;
using System.Net.Sockets;
using System.Text;
using BizFlow.Infrastructure.Authentication;
using MimeKit;

namespace BizFlow.IntegrationTests.Authentication;

public sealed class ResetEmailTests
{
    [Fact]
    public async Task SMTP_adapter_delivers_link_as_fragment_without_query_credentials()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var receive = ReceiveAsync(listener, deadline.Token);
        var sender = new PasswordResetEmailSender(new ResetEmailSettings
        {
            Enabled = true, Host = "127.0.0.1", Port = port, AllowInsecureLoopback = true,
            FromAddress = "no-reply@example.test", FrontendOrigin = "https://bizflow.example.test"
        });
        await sender.SendAsync("employee@example.test", "test-only-reset-token", deadline.Token);
        var message = await receive;
        Assert.Equal("employee@example.test", Assert.Single(message.To.Mailboxes).Address);
        Assert.Contains("https://bizflow.example.test/reset-password#token=test-only-reset-token", message.TextBody);
        Assert.DoesNotContain("?token=", message.TextBody);
    }

    [Theory]
    [InlineData("mail.example.test", "https://bizflow.example.test", true)]
    [InlineData("127.0.0.1", "http://public.example.test", true)]
    public void Unsafe_email_configuration_is_rejected(string host, string frontend, bool insecure)
    {
        Assert.False(new ResetEmailSettings
        { Enabled = true, Host = host, FrontendOrigin = frontend, FromAddress = "no-reply@example.test", AllowInsecureLoopback = insecure }.IsValid);
    }

    private static async Task<MimeMessage> ReceiveAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using var connection = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = connection.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
        await writer.WriteLineAsync("220 localhost test SMTP");
        MimeMessage? message = null;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase)) await writer.WriteLineAsync("250-localhost\r\n250 SIZE 1048576");
            else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("354 End with dot");
                var content = new StringBuilder();
                while (await reader.ReadLineAsync(cancellationToken) is { } data && data != ".")
                    content.Append(data.StartsWith("..", StringComparison.Ordinal) ? data[1..] : data).Append("\r\n");
                using var buffer = new MemoryStream(Encoding.UTF8.GetBytes(content.ToString()));
                message = await MimeMessage.LoadAsync(buffer, cancellationToken);
                await writer.WriteLineAsync("250 Accepted");
            }
            else if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
            { await writer.WriteLineAsync("221 Bye"); break; }
            else await writer.WriteLineAsync("250 OK");
        }
        return message ?? throw new InvalidOperationException("No test message was received.");
    }
}
