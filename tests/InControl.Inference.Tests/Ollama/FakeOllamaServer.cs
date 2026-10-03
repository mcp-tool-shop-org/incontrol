using System.Net;
using System.Net.Sockets;
using System.Text;

namespace InControl.Inference.Ollama;

/// <summary>
/// A loopback HTTP server that answers like Ollama with a chunked ndjson body, so a test
/// controls exactly when each piece of a reply arrives. No real Ollama is involved.
/// </summary>
internal sealed class FakeOllamaServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private readonly Func<string, Func<string, Task>, Task> _handler;

    /// <param name="handler">Gets the request path and a writer for one ndjson line.</param>
    public FakeOllamaServer(Func<string, Func<string, Task>, Task> handler)
    {
        _handler = handler;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
        _ = Task.Run(AcceptLoop);
    }

    public string Url { get; }

    public static string ChatLine(string content, bool done = false) =>
        "{\"model\":\"m\",\"created_at\":\"2025-01-01T00:00:00Z\",\"message\":{\"role\":\"assistant\",\"content\":\""
        + content + "\"},\"done\":" + (done ? "true" : "false") + "}";

    public static string PullLine(string status) => "{\"status\":\"" + status + "\"}";

    private async Task AcceptLoop()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                _ = Task.Run(() => Handle(client));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task Handle(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var path = await ReadRequestAsync(stream);

                var head = "HTTP/1.1 200 OK\r\nContent-Type: application/x-ndjson\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(head), _stop.Token);

                await _handler(path, async line =>
                {
                    var data = Encoding.UTF8.GetBytes(line + "\n");
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(data.Length.ToString("x") + "\r\n"), _stop.Token);
                    await stream.WriteAsync(data, _stop.Token);
                    await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"), _stop.Token);
                    await stream.FlushAsync(_stop.Token);
                });

                await stream.WriteAsync(Encoding.ASCII.GetBytes("0\r\n\r\n"), _stop.Token);
            }
            catch (Exception)
            {
                // The client went away, or the test ended. Nothing to report.
            }
        }
    }

    private async Task<string> ReadRequestAsync(NetworkStream stream)
    {
        var header = new StringBuilder();
        var one = new byte[1];
        while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            if (await stream.ReadAsync(one, _stop.Token) == 0)
                break;
            header.Append((char)one[0]);
        }

        var text = header.ToString();
        var length = 0;
        foreach (var line in text.Split("\r\n"))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                length = int.Parse(line["Content-Length:".Length..].Trim());
        }

        var body = new byte[length];
        var read = 0;
        while (read < length)
        {
            var n = await stream.ReadAsync(body.AsMemory(read), _stop.Token);
            if (n == 0)
                break;
            read += n;
        }

        var requestLine = text.Split("\r\n")[0];
        return requestLine.Split(' ')[1];
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        _stop.Dispose();
    }
}
