using System.Net.Sockets;
using ModelGenerator.Host.Protocol;

namespace ModelGenerator.Host;

/// <summary>Accepts clients on a Unix domain socket path and runs one <see cref="JsonRpcSession"/>
/// per connection. Removes a stale socket file on start.</summary>
public sealed class UnixSocketServer
{
    private readonly HostService _service;
    private readonly string _socketPath;

    public UnixSocketServer(HostService service, string socketPath)
    {
        _service = service;
        _socketPath = socketPath;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        string? dir = Path.GetDirectoryName(_socketPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (File.Exists(_socketPath))
        {
            File.Delete(_socketPath);
        }

        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
        listener.Listen(backlog: 8);

        Console.Error.WriteLine($"ModelGenerator.Host listening on {_socketPath} (protocol {HostProtocol.Version})");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Socket client;
                try
                {
                    client = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await using var stream = new NetworkStream(client, ownsSocket: true);
                        var session = new JsonRpcSession(_service, stream, stream);
                        await session.RunAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Client session ended: {ex.Message}");
                    }
                }, cancellationToken);
            }
        }
        finally
        {
            try
            {
                if (File.Exists(_socketPath))
                {
                    File.Delete(_socketPath);
                }
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
