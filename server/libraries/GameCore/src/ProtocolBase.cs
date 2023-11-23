using System;
using System.Threading;
using System.Threading.Tasks;

namespace GameCore
{
    public abstract class ProtocolBase
    {
    }

    public interface IProtocolSession
    {
        ValueTask SendAsync(ProtocolBase protocol, CancellationToken cancellationToken);
    }

    public interface IProtocolHandler
    {
        ValueTask HandlerAsync(IProtocolSession session, ProtocolBase protocol, CancellationToken cancellationToken);
    }

    public interface IProtocolHandler<TSession> where TSession : IProtocolSession
    {
        ValueTask HandlerAsync(TSession session, ProtocolBase protocol, CancellationToken cancellationToken);
    }

    public abstract class ProtocolHandlerBase<TProtocol> : IProtocolHandler
        where TProtocol : ProtocolBase
    {
        ValueTask IProtocolHandler.HandlerAsync(IProtocolSession session, ProtocolBase protocol, CancellationToken cancellationToken)
            => HandlerAsync(session, (TProtocol)protocol, cancellationToken);

        protected abstract ValueTask HandlerAsync(IProtocolSession session, TProtocol protocol, CancellationToken cancellationToken);
    }

    public abstract class ProtocolHandlerBase<TSession, TProtocol> : IProtocolHandler<TSession>
        where TSession : IProtocolSession
        where TProtocol : ProtocolBase
    {
        ValueTask IProtocolHandler<TSession>.HandlerAsync(TSession session, ProtocolBase protocol, CancellationToken cancellationToken)
            => HandlerAsync(session, (TProtocol)protocol, cancellationToken);

        protected abstract ValueTask HandlerAsync(TSession session, TProtocol protocol, CancellationToken cancellationToken);
    }

    public interface IProtocolHandlerDispatcher
    {
        ValueTask DispatchAsync(IProtocolSession session, ProtocolBase protocol, CancellationToken cancellationToken);
    }

    public interface IProtocolHandlerDispatcher<TSession> where TSession : IProtocolSession
    {
        ValueTask DispatchAsync(TSession session, ProtocolBase protocol, CancellationToken cancellationToken);
    }

    public sealed class DefaultProtocolHandlerDispatcher : IProtocolHandlerDispatcher
    {
        readonly IServiceProvider _provider;

        public DefaultProtocolHandlerDispatcher(IServiceProvider provider) => _provider = provider;

        public ValueTask DispatchAsync(IProtocolSession session, ProtocolBase protocol, CancellationToken cancellationToken)
        {
            var handlerType = typeof(ProtocolHandlerBase<>).MakeGenericType(protocol.GetType());
            if (_provider.GetService(handlerType) is IProtocolHandler handler)
                return handler.HandlerAsync(session, protocol, cancellationToken);
            return default;
        }
    }

    public sealed class DefaultProtocolHandlerDispatcher<TSession> : IProtocolHandlerDispatcher<TSession> where TSession : IProtocolSession
    {
        readonly IServiceProvider _provider;

        public DefaultProtocolHandlerDispatcher(IServiceProvider provider) => _provider = provider;

        public ValueTask DispatchAsync(TSession session, ProtocolBase protocol, CancellationToken cancellationToken)
        {
            var handlerType = typeof(ProtocolHandlerBase<,>).MakeGenericType(typeof(TSession), protocol.GetType());
            if (_provider.GetService(handlerType) is IProtocolHandler<TSession> handler)
                return handler.HandlerAsync(session, protocol, cancellationToken);
            return default;
        }
    }
}
