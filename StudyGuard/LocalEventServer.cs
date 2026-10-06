using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StudyGuard
{
    public class LocalEventServer
    {
        private TcpListener? listener;
        private CancellationTokenSource? cancellation;

        private readonly ConfigService configService;

        public event Action<string>? ViolationReceived;

        public LocalEventServer(ConfigService configService)
        {
            this.configService = configService;
        }

        public void Start()
        {
            listener = new TcpListener(IPAddress.Loopback, 8765);
            listener.Start();

            cancellation = new CancellationTokenSource();

            _ = Task.Run(() => ListenLoop(cancellation.Token));
        }

        public void Stop()
        {
            cancellation?.Cancel();
            listener?.Stop();
        }

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    TcpClient client =
                        await listener!.AcceptTcpClientAsync(token);

                    _ = Task.Run(() => HandleClient(client));
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                }
            }
        }

        private async Task HandleClient(TcpClient client)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            using (StreamReader reader =
                   new StreamReader(stream, Encoding.UTF8))
            {
                string? requestLine = await reader.ReadLineAsync();

                if (requestLine == null)
                    return;

                string[] parts = requestLine.Split(' ');

                if (parts.Length < 2)
                    return;

                string path = parts[1];

                // -------------------------
                // İHLAL BİLDİRİMİ
                // -------------------------

                if (path.StartsWith("/violation"))
                {
                    string url = "";

                    int index = path.IndexOf("?url=");

                    if (index >= 0)
                    {
                        string encodedUrl =
                            path[(index + 5)..];

                        url = Uri.UnescapeDataString(encodedUrl);
                    }

                    ViolationReceived?.Invoke(url);

                    await SendResponse(
                        stream,
                        "text/plain",
                        "OK"
                    );

                    return;
                }

                // -------------------------
                // CONFIG BİLGİSİ
                // -------------------------

                if (path.StartsWith("/config"))
                {
                    var responseObject = new
                    {
                        message =
                            configService.Config.BlockMessage,

                        deviceName =
                            configService.Config.DeviceName,

                        studyModeEnabled =
                            configService.Config.StudyModeEnabled
                    };

                    string json =
                        JsonSerializer.Serialize(responseObject);

                    await SendResponse(
                        stream,
                        "application/json",
                        json
                    );

                    return;
                }

                await SendResponse(
                    stream,
                    "text/plain",
                    "Not Found",
                    "404 Not Found"
                );
            }
        }

        private async Task SendResponse(
            NetworkStream stream,
            string contentType,
            string body,
            string status = "200 OK")
        {
            byte[] bodyBytes =
                Encoding.UTF8.GetBytes(body);

            string headers =
                $"HTTP/1.1 {status}\r\n" +
                $"Content-Type: {contentType}; charset=utf-8\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                $"Content-Length: {bodyBytes.Length}\r\n" +
                "Connection: close\r\n" +
                "\r\n";

            byte[] headerBytes =
                Encoding.UTF8.GetBytes(headers);

            await stream.WriteAsync(headerBytes);
            await stream.WriteAsync(bodyBytes);
        }
    }
}