using System.Text.Json;
using GameCore;
using Orleans.Utilities;

namespace GameServer;

[RegisterConverter]
public sealed class C2S_ClientLoginConverter : ConverterBase<C2S_ClientLogin> { }

[RegisterConverter]
public sealed class S2C_ClientLoginConverter : ConverterBase<S2C_ClientLogin> { }

sealed class C2S_ClientLoginHandler(
    ILogger<C2S_ClientLoginHandler> _logger,
    IClusterClient _clusterClient)
    : ProtocolHandlerBase<WebSocketProtocolSession, C2S_ClientLogin>
{
    protected override async ValueTask HandlerAsync(WebSocketProtocolSession session, C2S_ClientLogin protocol, CancellationToken cancellationToken)
    {
        _logger.LogDebug(nameof(HandlerAsync));

        var player = _clusterClient.GetGrain<IPlayer>(1);
        var send = await player.LoginAsync(session.GrainReference, protocol);
        await session.SendAsync(send, cancellationToken);
    }
}

[RegisterConverter]
public sealed class C2S_HeartbeatConverter : ConverterBase<C2S_Heartbeat> { }

[RegisterConverter]
public sealed class S2C_HeartbeatConverter : ConverterBase<S2C_Heartbeat> { }

sealed class C2S_HeartbeatHandler(
    ILogger<C2S_HeartbeatHandler> _logger,
    IClusterClient _clusterClient)
    : ProtocolHandlerBase<WebSocketProtocolSession, C2S_Heartbeat>
{
    protected override async ValueTask HandlerAsync(WebSocketProtocolSession session, C2S_Heartbeat protocol, CancellationToken cancellationToken)
    {
        _logger.LogDebug(nameof(HandlerAsync));

        var player = _clusterClient.GetGrain<IPlayer>(1);
        await player.HeartbeatAsync(session.GrainReference, protocol);
    }
}

public interface IGrainsProtocolSession : IGrainObserver
{
    ValueTask SendAsync(ProtocolBase protocol);
}

public interface IPlayer : IGrainWithIntegerKey
{
    ValueTask<S2C_ClientLogin> LoginAsync(IGrainsProtocolSession session, C2S_ClientLogin receive);
    ValueTask HeartbeatAsync(IGrainsProtocolSession session, C2S_Heartbeat receive);
}

public sealed class Player(ILogger<Player> _logger) : Grain, IPlayer
{
    readonly ObserverManager<IGrainsProtocolSession> _sessions = new(TimeSpan.FromSeconds(30), _logger);

    CancellationTokenSource? _heartbeatCancellationTokenSource;

    public async ValueTask<S2C_ClientLogin> LoginAsync(IGrainsProtocolSession session, C2S_ClientLogin receive)
    {
        await Task.Yield();
        if (receive.Account == "overing")
        {
            _sessions.Subscribe(session, session);

            _heartbeatCancellationTokenSource ??= new();
            _ = CheckHeartbeatAsync(_heartbeatCancellationTokenSource.Token);
            return new S2C_ClientLogin { Name = "Overing" };
        }
        return new S2C_ClientLogin { Error = ClientLoginError.AccountNotFound };
    }

    public ValueTask HeartbeatAsync(IGrainsProtocolSession session, C2S_Heartbeat receive)
    {
        _sessions.Subscribe(session, session);
        DelayDeactivation(TimeSpan.FromMinutes(10));
        return ValueTask.CompletedTask;
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _heartbeatCancellationTokenSource?.Cancel();
        return base.OnDeactivateAsync(reason, cancellationToken);
    }

    async ValueTask CheckHeartbeatAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            _sessions.Notify(s => s.SendAsync(new S2C_Heartbeat()));
        }
    }
}

[GenerateSerializer]
public record struct ConverterData(byte[] Raw);

public abstract class ConverterBase<T> : IConverter<T, ConverterData>
{
    static readonly JsonSerializerOptions _options = new()
    {
        IncludeFields = true,
        IgnoreReadOnlyFields = true,
        IgnoreReadOnlyProperties = true,
        TypeInfoResolver = new ProtocolJsonTypeInfoResolver(),
    };

    public T ConvertFromSurrogate(in ConverterData surrogate) => JsonSerializer.Deserialize<T>(new MemoryStream(surrogate.Raw), _options)!;

    public ConverterData ConvertToSurrogate(in T value) => new(JsonSerializer.SerializeToUtf8Bytes(value, _options));
}
