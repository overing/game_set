using System.Net.WebSockets;
using System.Text.Json;
using GameCore;

namespace GameServer;

public sealed class WebSocketProtocolSocketSession(
    ILogger<WebSocketProtocolSocketSession> _logger,
    WebSocket _webSocket,
    IProtocolHandlerDispatcher _dispatcher)
    : IProtocolSession
{
    readonly MemoryStream _receiveBuffer = new();
    readonly MemoryStream _sendBuffer = new();

    public static async Task HandleWebSocketAsync(HttpContext context, Func<Task> _)
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            var services = context.RequestServices;
            var logger = services.GetRequiredService<ILogger<WebSocketProtocolSocketSession>>();
            var dispatcher = services.GetRequiredService<IProtocolHandlerDispatcher>();
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var session = new WebSocketProtocolSocketSession(logger, webSocket, dispatcher);
            await session.HandleReceiveAsync(context.RequestAborted);
        }
        else
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }

    public async ValueTask HandleReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = WebSocket.CreateServerBuffer(ushort.MaxValue);
        while (_webSocket.State == WebSocketState.Open)
        {
            _receiveBuffer.SetLength(0);
            WebSocketReceiveResult receive;
            do
            {
                receive = await _webSocket.ReceiveAsync(buffer, cancellationToken);
                if (receive.Count > 0)
                    _receiveBuffer.Write(buffer.Array!, 0, receive.Count);
            }
            while (!receive.EndOfMessage);
            if (_receiveBuffer.Length == 0)
                continue;

            _receiveBuffer.Position = 0;

            var options = ProtocolBase.JsonSerializerOptions;
            var protocol = await JsonSerializer.DeserializeAsync<ProtocolBase>(_receiveBuffer, options, cancellationToken);
            if (protocol is not null)
                await _dispatcher.DispatchAsync(this, protocol, cancellationToken);
        }
        _logger.LogInformation("WebSocket Close");
    }

    public async ValueTask SendAsync(ProtocolBase protocol, CancellationToken cancellationToken)
    {
        _sendBuffer.SetLength(0);
        var options = ProtocolBase.JsonSerializerOptions;
        await JsonSerializer.SerializeAsync(_sendBuffer, protocol, options, cancellationToken);
        _sendBuffer.Position = 0;
        var data = _sendBuffer.ToArray();
        await _webSocket.SendAsync(data, WebSocketMessageType.Text, endOfMessage: true, cancellationToken: cancellationToken);
    }
}
