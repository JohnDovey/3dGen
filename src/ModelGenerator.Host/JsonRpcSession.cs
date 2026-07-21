using System.Text;
using System.Text.Json;
using ModelGenerator.Host.Protocol;

namespace ModelGenerator.Host;

/// <summary>Reads NDJSON RPC requests from a stream and writes responses. One connection = one
/// session; safe to use on a Unix socket accept loop or stdio.</summary>
public sealed class JsonRpcSession
{
    private readonly HostService _service;
    private readonly TextReader _reader;
    private readonly TextWriter _writer;

    public JsonRpcSession(HostService service, Stream input, Stream output)
    {
        _service = service;
        _reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        _writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\n"
        };
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (line is null)
            {
                break; // EOF
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var response = HandleLine(line);
            string json = JsonSerializer.Serialize(response, HostProtocol.JsonOptions);
            await _writer.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
    }

    public RpcResponse HandleLine(string line)
    {
        RpcRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<RpcRequest>(line, HostProtocol.JsonOptions);
        }
        catch (JsonException ex)
        {
            return new RpcResponse
            {
                Id = null,
                Error = new RpcError { Code = RpcErrorCodes.ParseError, Message = ex.Message }
            };
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Method))
        {
            return new RpcResponse
            {
                Id = request?.Id,
                Error = new RpcError { Code = RpcErrorCodes.InvalidRequest, Message = "method is required." }
            };
        }

        try
        {
            object result = _service.Dispatch(request.Method, request.Params);
            return new RpcResponse { Id = request.Id, Result = result };
        }
        catch (HostMethodNotFoundException ex)
        {
            return new RpcResponse
            {
                Id = request.Id,
                Error = new RpcError { Code = RpcErrorCodes.MethodNotFound, Message = ex.Message }
            };
        }
        catch (HostInvalidParamsException ex)
        {
            return new RpcResponse
            {
                Id = request.Id,
                Error = new RpcError { Code = RpcErrorCodes.InvalidParams, Message = ex.Message }
            };
        }
        catch (Exception ex)
        {
            return new RpcResponse
            {
                Id = request.Id,
                Error = new RpcError { Code = RpcErrorCodes.InternalError, Message = ex.Message }
            };
        }
    }
}
