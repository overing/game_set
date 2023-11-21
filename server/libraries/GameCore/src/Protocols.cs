
namespace GameCore
{
    public sealed class C2S_ClientLogin : ProtocolBase
    {
        public string Account { get; set; }
    }

    public enum ClientLoginError : byte
    {
        None = default,
        AccountNotFound,
    }

    public sealed class S2C_ClientLogin : ProtocolBase
    {
        public ClientLoginError Error { get; set; }
        public string Name { get; set; }
    }

    public sealed class S2C_Heartbeat : ProtocolBase
    {
        
    }

    public sealed class C2S_Heartbeat : ProtocolBase
    {
        
    }
}