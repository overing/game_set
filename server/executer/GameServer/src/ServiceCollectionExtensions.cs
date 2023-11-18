using GameCore;

namespace GameServer;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProtocolHandler(this IServiceCollection collection)
    {
        var baseType = typeof(IProtocolHandler);
        var query = typeof(Program).Assembly.GetTypes()
            .Where(baseType.IsAssignableFrom)
            .Where(t => !t.IsAbstract)
            .Where(t => t.BaseType != null)
            .Select(t => new ServiceDescriptor(t.BaseType!, t, ServiceLifetime.Transient));
        foreach (var descriptor in query)
            collection.Add(descriptor);
        collection.AddSingleton<IProtocolHandlerDispatcher, DefaultProtocolHandlerDispatcher>();
        return collection;
    }
}
