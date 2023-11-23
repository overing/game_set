using GameCore;

namespace GameServer;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProtocolHandler<TSession>(this IServiceCollection collection)
        where TSession : IProtocolSession
    {
        var baseType = typeof(IProtocolHandler<TSession>);
        var query = typeof(Program).Assembly.GetTypes()
            .Where(baseType.IsAssignableFrom)
            .Where(t => !t.IsAbstract)
            .Where(t => t.BaseType != null)
            .Select(t => new ServiceDescriptor(t.BaseType!, t, ServiceLifetime.Transient));
        foreach (var descriptor in query)
            collection.Add(descriptor);
        collection.AddSingleton<IProtocolHandlerDispatcher<TSession>, DefaultProtocolHandlerDispatcher<TSession>>();
        return collection;
    }
}
