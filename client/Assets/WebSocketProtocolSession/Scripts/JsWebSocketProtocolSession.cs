using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GameCore;
using UnityEngine;

namespace WebSocketProtocolSession
{
    using JsWebSockets;

    public sealed class JsWebSocketProtocolSession : IWebSocketProtocolSession
    {
        readonly string _url;
        JsWebSocket _webSocket;
        bool _disposed;
        IProtocolHandlerDispatcher<IWebSocketProtocolSession> _dispatcher;

        public bool IsConnected { get; private set; }

        public JsWebSocketProtocolSession(string url, IProtocolHandlerDispatcher<IWebSocketProtocolSession> dispatcher)
        {
            _url = url;
            _dispatcher = dispatcher;
        }

        public async ValueTask ConnectAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            var webSocket = new JsWebSocket(_url);
            webSocket.OnOpen += onConnectOpen;
            webSocket.OnClose += onConnectClose;
            webSocket.OnError += onConnectError;

            try
            {
                webSocket.Connect();
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
            IsConnected = true;

            void onConnectOpen(object sender, EventArgs args) => tcs.SetResult(true);

            void onConnectClose(object sender, CloseEventArgs args) => tcs.SetCanceled();

            void onConnectError(object sender, ErrorEventArgs args)
            {
                var ex = new Exception(args.Error);
                Debug.Log(nameof(onConnectError));
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
            var options = ProtocolJsonSerialization.Options;
            var protocol = JsonSerializer.Deserialize<ProtocolBase>(args.Data, options);
            _ = _dispatcher.DispatchAsync(this, protocol, default);
        }

        void OnWebSocketError(object sender, ErrorEventArgs args)
        {
            var ex = new Exception(args.Error);
            Debug.LogErrorFormat("{0}: {1}", nameof(OnWebSocketError), ex.Message);
            Debug.LogException(ex);
        }

        public ValueTask SendAsync(ProtocolBase protocol, CancellationToken cancellationToken)
        {
            var options = ProtocolJsonSerialization.Options;
            var json = JsonSerializer.Serialize(protocol, options);
            _webSocket.Send(json);
            return default;
        }

        void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (_webSocket is JsWebSocket webSocket)
                {
                    IsConnected = false;
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

    namespace JsWebSockets
    {
        public sealed class CloseEventArgs : EventArgs
        {
            public ushort Code { get; }
            public string Reason { get; }
            public bool WasClean { get; }
            public CloseEventArgs(ushort code, string reason, bool wasClean)
            {
                Code = code;
                Reason = reason;
                WasClean = wasClean;
            }
        }

        public sealed class MessageEventArgs : EventArgs
        {
            public string Data { get; }
            public MessageEventArgs(string data) => Data = data;
        }

        public sealed class ErrorEventArgs : EventArgs
        {
            public string Error { get; }
            public ErrorEventArgs(string error) => Error = error;
        }

        public sealed class JsWebSocket : IJsWebSocket, IDisposable
        {
            int _id;
            bool _disposed;
            public event EventHandler<EventArgs> OnOpen;
            public event EventHandler<MessageEventArgs> OnMessage;
            public event EventHandler<ErrorEventArgs> OnError;
            public event EventHandler<CloseEventArgs> OnClose;

            public JsWebSocket(string url) => _id = JsNative.CreateHandler(url, this);

            void IJsWebSocket.ReceiveOpen(EventArgs args) => OnOpen?.Invoke(this, args);
            void IJsWebSocket.ReceiveMessage(MessageEventArgs args) => OnMessage?.Invoke(this, args);
            void IJsWebSocket.ReceiveError(ErrorEventArgs args) => OnError?.Invoke(this, args);
            void IJsWebSocket.ReceiveClose(CloseEventArgs args) => OnClose?.Invoke(this, args);

            public void Connect()
            {
                if (_id > 0)
                    JsNative.JsConnect(_id);
            }

            public void Send(string data)
            {
                if (_id > 0)
                    JsNative.JsSend(_id, data);
            }

            public void Close()
            {
                if (_id > 0)
                {
                    JsNative.Release(_id);
                    _id = 0;
                }
            }

            void Dispose(bool disposing)
            {
                if (_disposed)
                    return;

                if (disposing)
                    Close();

                _disposed = true;
            }

            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }

        interface IJsWebSocket
        {
            void ReceiveOpen(EventArgs args);
            void ReceiveMessage(MessageEventArgs args);
            void ReceiveError(ErrorEventArgs args);
            void ReceiveClose(CloseEventArgs args);
        }

        static class JsNative
        {
            static readonly Dictionary<int, IJsWebSocket> s_instances = new();

            public static int CreateHandler(string url, IJsWebSocket instance)
            {
                var id = JsCreate(url);
                JsAddOpenEventListener(id, JsOnOpen);
                JsAddMessageEventListener(id, JsOnMessage);
                JsAddErrorEventListener(id, JsOnError);
                JsAddCloseEventListener(id, JsOnClose);
                s_instances[id] = instance;
                return id;
            }

            public static void Release(int id)
            {
                JsClose(id);
                s_instances.Remove(id);
            }

            [System.Runtime.InteropServices.DllImport("__Internal")]
            static extern int JsCreate(string url);

            [System.Runtime.InteropServices.DllImport("__Internal")]
            public static extern int JsConnect(int id);

            [System.Runtime.InteropServices.DllImport("__Internal")]
            public static extern int JsSend(int id, string data);

            [System.Runtime.InteropServices.DllImport("__Internal")]
            static extern int JsClose(int id);

            delegate void OpenCallback(int id);
            [System.Runtime.InteropServices.DllImport("__Internal")]
            static extern void JsAddOpenEventListener(int id, OpenCallback callback);
            [AOT.MonoPInvokeCallback(typeof(OpenCallback))]
            static void JsOnOpen(int id)
            {
                if (s_instances.TryGetValue(id, out var instance))
                    instance.ReceiveOpen(EventArgs.Empty);
            }

            delegate void MessageCallback(int id, string data);
            [System.Runtime.InteropServices.DllImport("__Internal")]
            static extern void JsAddMessageEventListener(int id, MessageCallback callback);
            [AOT.MonoPInvokeCallback(typeof(MessageCallback))]
            static void JsOnMessage(int id, string data)
            {
                if (s_instances.TryGetValue(id, out var instance))
                    instance.ReceiveMessage(new MessageEventArgs(data));
            }

            delegate void ErrorCallback(int id, string error);
            [System.Runtime.InteropServices.DllImport("__Internal")]
            static extern void JsAddErrorEventListener(int id, ErrorCallback callback);
            [AOT.MonoPInvokeCallback(typeof(ErrorCallback))]
            static void JsOnError(int id, string data)
            {
                if (s_instances.TryGetValue(id, out var instance))
                    instance.ReceiveError(new ErrorEventArgs(data));
            }

            delegate void CloseCallback(int id, ushort code, string reason, int wasClean);
            [System.Runtime.InteropServices.DllImport("__Internal")]
            static extern void JsAddCloseEventListener(int id, CloseCallback callback);
            [AOT.MonoPInvokeCallback(typeof(CloseCallback))]
            static void JsOnClose(int id, ushort code, string reason, int wasClean)
            {
                if (s_instances.TryGetValue(id, out var instance))
                    instance.ReceiveClose(new CloseEventArgs(code, reason, wasClean > 0));
            }
        }
    }
}
