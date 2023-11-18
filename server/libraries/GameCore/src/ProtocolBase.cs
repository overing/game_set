using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GameCore
{
    public abstract class ProtocolBase
    {
        public static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions
        {
            IgnoreReadOnlyProperties = true,
            TypeInfoResolver = new ProtocolJsonTypeInfoResolver(),
        };
    }

    public interface IProtocolSession
    {
        ValueTask SendAsync(ProtocolBase protocol, CancellationToken cancellationToken);
    }

    public interface IProtocolHandler
    {
        ValueTask HandlerAsync(IProtocolSession session, ProtocolBase protocol, CancellationToken cancellationToken);
    }

    public abstract class ProtocolHandlerBase<TProtocol> : IProtocolHandler where TProtocol : ProtocolBase
    {
        ValueTask IProtocolHandler.HandlerAsync(IProtocolSession session, ProtocolBase protocol, CancellationToken cancellationToken)
            => HandlerAsync(session, (TProtocol)protocol, cancellationToken);

        protected abstract ValueTask HandlerAsync(IProtocolSession session, TProtocol protocol, CancellationToken cancellationToken);
    }

    public interface IProtocolHandlerDispatcher
    {
        ValueTask DispatchAsync(IProtocolSession session, ProtocolBase protocol, CancellationToken cancellationToken);
    }

    public sealed class DefaultProtocolHandlerDispatcher : IProtocolHandlerDispatcher
    {
        IServiceProvider _provider;

        public DefaultProtocolHandlerDispatcher(IServiceProvider provider) => _provider = provider;

        public ValueTask DispatchAsync(IProtocolSession session, ProtocolBase protocol, CancellationToken cancellationToken)
        {
            var handlerType = typeof(ProtocolHandlerBase<>).MakeGenericType(protocol.GetType());
            if (_provider.GetService(handlerType) is IProtocolHandler handler)
                return handler.HandlerAsync(session, protocol, cancellationToken);
            return default;
        }
    }
}
