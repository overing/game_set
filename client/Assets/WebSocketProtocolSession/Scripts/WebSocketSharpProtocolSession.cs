
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GameCore;
using UnityEngine;
using WebSocketSharp;

namespace WebSocketProtocolSession
{
    public sealed class WebSocketSharpProtocolSession : IWebSocketProtocolSession
    {
        readonly string _url;
        readonly System.IO.MemoryStream _sendBUffer;
        WebSocket _webSocket;
        bool _disposed;
        IProtocolHandlerDispatcher _dispatcher;

        public WebSocketSharpProtocolSession(string url, IProtocolHandlerDispatcher dispatcher)
        {
            _url = url;
            _sendBUffer = new();
            _dispatcher = dispatcher;
        }

        public async ValueTask ConnectAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            var webSocket = new WebSocket(_url);
            webSocket.OnOpen += onConnectOpen;
            webSocket.OnClose += onConnectClose;
            webSocket.OnError += onConnectError;

            try
            {
                webSocket.ConnectAsync();
                await tcs.Task;
            }
            catch
            {
                throw;
            }
            finally
            {
                webSocket.OnOpen -= onConnectOpen;
                webSocket.OnClose -= onConnectClose;
                webSocket.OnError -= onConnectError;
            }

            webSocket.OnClose += OnWebSocketClose;
            webSocket.OnMessage += OnWebSocketMessage;
            webSocket.OnError += OnWebSocketError;
            _webSocket = webSocket;

            void onConnectOpen(object sender, EventArgs args) => tcs.SetResult(true);

            void onConnectClose(object sender, CloseEventArgs args) => tcs.SetCanceled();

            void onConnectError(object sender, ErrorEventArgs args)
            {
                var ex = args.Exception;
                Debug.LogErrorFormat("{0}: {1}", nameof(onConnectError), ex.Message);
                Debug.LogException(ex);
                tcs.SetException(ex);
            }
        }

        void OnWebSocketClose(object sender, CloseEventArgs args)
        {
            Debug.LogWarningFormat("{0}: {1}", nameof(OnWebSocketClose), new { args.Code, args.Reason });
        }

        void OnWebSocketMessage(object sender, MessageEventArgs args)
        {
            var protocol = JsonSerializer.Deserialize<ProtocolBase>(args.Data, ProtocolBase.JsonSerializerOptions);
            _ = _dispatcher.DispatchAsync(this, protocol, default);
        }

        void OnWebSocketError(object sender, ErrorEventArgs args)
        {
            var ex = args.Exception;
            Debug.LogErrorFormat("{0}: {1}", nameof(OnWebSocketError), ex.Message);
            Debug.LogException(ex);
        }

        public async ValueTask SendAsync(ProtocolBase protocol, CancellationToken cancellationToken)
        {
            var buffer = _sendBUffer;
            buffer.SetLength(0);
            var options = ProtocolBase.JsonSerializerOptions;
            await JsonSerializer.SerializeAsync(buffer, protocol, options, cancellationToken);
            buffer.Position = 0;

            var tcs = new TaskCompletionSource<bool>();
            _webSocket.SendAsync(buffer, (int)buffer.Length, tcs.SetResult);
            await tcs.Task;
        }

        void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (_webSocket is WebSocket webSocket)
                {
                    webSocket.OnError -= OnWebSocketError;
                    webSocket.OnMessage -= OnWebSocketMessage;
                    webSocket.OnClose -= OnWebSocketClose;
                    webSocket.Close();
                    _webSocket = null;
                }
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
