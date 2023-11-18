using GameCore;

namespace GameServer;

sealed class C2S_ClientLoginHandler(ILogger<C2S_ClientLoginHandler> _logger) : ProtocolHandlerBase<C2S_ClientLogin>
{
    protected override async ValueTask HandlerAsync(IProtocolSession session, C2S_ClientLogin protocol, CancellationToken cancellationToken)
    {
        _logger.LogDebug(nameof(HandlerAsync));

        if (protocol.Account == "overing")
            await session.SendAsync(new S2C_ClientLogin { Name = "Overing" }, cancellationToken);
        else
            await session.SendAsync(new S2C_ClientLogin { Error = ClientLoginError.AccountNotFound }, cancellationToken);
    }
}

