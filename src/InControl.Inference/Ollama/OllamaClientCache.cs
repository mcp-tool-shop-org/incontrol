using OllamaSharp;
using InControl.Core.Compute;

namespace InControl.Inference.Ollama;

/// <summary>
/// Holds the Ollama client for the current endpoint URL. A call takes a lease for as long as it
/// uses the client. When the URL changes, new calls get a new client and the old one is disposed
/// once its last lease is released, so a stream in flight is never cut by an endpoint change.
/// </summary>
internal sealed class OllamaClientCache
{
    private readonly IOllamaEndpoint _endpoint;
    private readonly object _gate = new();
    private Entry? _current;

    public OllamaClientCache(IOllamaEndpoint endpoint)
    {
        _endpoint = endpoint;
        _endpoint.Changed += (_, _) => Refresh();
    }

    public Lease Acquire()
    {
        var url = _endpoint.BaseUrl;
        lock (_gate)
        {
            if (_current is null || !string.Equals(_current.Url, url, StringComparison.Ordinal))
            {
                Retire(_current);
                _current = Entry.Create(url);
            }

            _current.Leases++;
            return new Lease(this, _current);
        }
    }

    /// <summary>
    /// Changed fires when a tunnel opens or closes and when "use this PC" is chosen again.
    /// Only a different URL needs a different client.
    /// </summary>
    private void Refresh()
    {
        var url = _endpoint.BaseUrl;
        lock (_gate)
        {
            if (_current is null || string.Equals(_current.Url, url, StringComparison.Ordinal))
                return;

            Retire(_current);
            _current = null;
        }
    }

    private static void Retire(Entry? entry)
    {
        if (entry is null)
            return;

        entry.Retired = true;
        if (entry.Leases == 0)
            entry.Dispose();
    }

    private void Release(Entry entry)
    {
        lock (_gate)
        {
            entry.Leases--;
            if (entry.Retired && entry.Leases == 0)
                entry.Dispose();
        }
    }

    internal sealed class Entry
    {
        private Entry(string url, OllamaApiClient client, HttpClient http)
        {
            Url = url;
            Client = client;
            Http = http;
        }

        public string Url { get; }
        public OllamaApiClient Client { get; }
        public HttpClient Http { get; }
        public int Leases { get; set; }
        public bool Retired { get; set; }
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Redirects are off so a 307 or 308 cannot replay a chat, health, pull, or delete onto
        /// another host. The client timeout is off too: it would also cut a long reply or a model
        /// pull. Callers bound the wait with <see cref="OllamaTimeouts"/> instead.
        /// </summary>
        public static Entry Create(string baseUrl)
        {
            HttpClientHandler? handler = null;
            HttpClient? http = null;
            try
            {
                handler = new HttpClientHandler { AllowAutoRedirect = false };
                http = new HttpClient(handler, disposeHandler: true)
                {
                    BaseAddress = new Uri(baseUrl),
                    Timeout = Timeout.InfiniteTimeSpan
                };
                handler = null;
                var client = new OllamaApiClient(http);
                var ownedHttp = http;
                http = null;
                return new Entry(baseUrl, client, ownedHttp);
            }
            finally
            {
                http?.Dispose();
                handler?.Dispose();
            }
        }

        /// <summary>
        /// The <see cref="HttpClient"/> overload of OllamaApiClient does not dispose the handler,
        /// so the client and the HttpClient (which owns the handler) are both disposed here.
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            try
            {
                Client.Dispose();
            }
            finally
            {
                Http.Dispose();
            }
        }
    }

    internal sealed class Lease : IDisposable
    {
        private readonly OllamaClientCache _owner;
        private readonly Entry _entry;
        private int _released;

        public Lease(OllamaClientCache owner, Entry entry)
        {
            _owner = owner;
            _entry = entry;
        }

        public OllamaApiClient Client => _entry.Client;

        internal Entry Entry => _entry;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                _owner.Release(_entry);
        }
    }
}

/// <summary>
/// Time limits for Ollama calls. A stream is bounded by how long it goes quiet, not by its total
/// length, so a long reply or pull is not cut while data is still arriving.
/// </summary>
internal static class OllamaTimeouts
{
    /// <summary>
    /// Zero or less means no limit.
    /// </summary>
    public static TimeSpan FromSeconds(int seconds) =>
        seconds > 0 ? TimeSpan.FromSeconds(seconds) : Timeout.InfiniteTimeSpan;

    /// <summary>
    /// Streams items from <paramref name="start"/>. The wait for the first item, and for each item
    /// after it, may not exceed <paramref name="idle"/>. Time the caller spends between items does not count.
    /// </summary>
    public static async IAsyncEnumerable<T> StreamAsync<T>(
        Func<CancellationToken, IAsyncEnumerable<T>> start,
        TimeSpan idle,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var guard = CancellationTokenSource.CreateLinkedTokenSource(ct);
        guard.CancelAfter(idle);

        await using var items = start(guard.Token).GetAsyncEnumerator(guard.Token);
        while (true)
        {
            bool has;
            try
            {
                has = await items.MoveNextAsync();
            }
            catch (OperationCanceledException) when (guard.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                throw new TimeoutException($"Ollama sent nothing for {idle.TotalSeconds:0} seconds.");
            }

            if (!has)
                yield break;

            yield return items.Current;
            guard.CancelAfter(idle);
        }
    }

    /// <summary>
    /// Runs one request and reply. The whole call may not exceed <paramref name="limit"/>.
    /// </summary>
    public static async Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> call,
        TimeSpan limit,
        CancellationToken ct)
    {
        using var guard = CancellationTokenSource.CreateLinkedTokenSource(ct);
        guard.CancelAfter(limit);
        try
        {
            return await call(guard.Token);
        }
        catch (OperationCanceledException) when (guard.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Ollama did not answer within {limit.TotalSeconds:0} seconds.");
        }
    }

    public static Task RunAsync(Func<CancellationToken, Task> call, TimeSpan limit, CancellationToken ct) =>
        RunAsync<object?>(async token =>
        {
            await call(token);
            return null;
        }, limit, ct);
}
