using System;
using System.Threading.Tasks;
using GameCore;

namespace WebSocketProtocolSession
{
    public interface IWebSocketProtocolSession : IProtocolSession, IDisposable
    {
        bool IsConnected { get; }
        ValueTask ConnectAsync();
    }
}