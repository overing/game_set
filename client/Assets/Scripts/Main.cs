using System;
using System.Threading;
using System.Threading.Tasks;
using GameCore;
using UnityEngine;
using WebSocketProtocolSession;

public sealed class Main : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod]
    static void OnLoadMethod() => DontDestroyOnLoad(new GameObject(nameof(Main), typeof(Main)));

    IServiceProvider _services;
    IProtocolHandlerDispatcher _dispatcher;
    IWebSocketProtocolSession _session;
    string _url = "ws://127.0.0.1:8763";
    string _account = "overing";
    bool _guiInitialized;

    public string PlayerName { get; set; }

    void Awake()
    {
        var services = new System.ComponentModel.Design.ServiceContainer();
        services.AddService(typeof(Main), this);
        services.AddService(typeof(IProtocolSession), (_, _) => _session);
        services.AddService(typeof(ProtocolHandlerBase<S2C_ClientLogin>), (p, _) => new S2C_ClientLoginHandler(p));
        services.AddService(typeof(ProtocolHandlerBase<S2C_Heartbeat>), (p, _) => new S2C_HeartbeatHandler(p));
        _services = services;
        _dispatcher = new DefaultProtocolHandlerDispatcher(services);
    }

    void OnGUI()
    {
        if (!_guiInitialized)
        {
            GUI.skin.label.fontSize = Screen.height / 18;
            GUI.skin.textField.fontSize = Screen.height / 20;
            GUI.skin.button.fontSize = Screen.height / 20;
            _guiInitialized = true;
        }

        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(Screen.width), GUILayout.Height(Screen.height));

        if (_session == null)
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Url", GUILayout.Width(192));
            _url = GUILayout.TextField(_url, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Account", GUILayout.Width(192));
            _account = GUILayout.TextField(_account, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Login"))
                _ = LoginAsync(_url, _account);
        }
        else
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Name", GUILayout.Width(192));
            GUILayout.Label(PlayerName, GUI.skin.textField, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
    }

    async ValueTask LoginAsync(string url, string account)
    {
        IWebSocketProtocolSession session;
        if (Application.isEditor || Application.platform != RuntimePlatform.WebGLPlayer)
            session = new WebSocketSharpProtocolSession(url, _dispatcher);
        else
            session = new JsWebSocketProtocolSession(url, _dispatcher);
        try
        {
            await session.ConnectAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.Message);
            Debug.LogException(ex);
            return;
        }

        _session = session;

        await session.SendAsync(new C2S_ClientLogin { Account = account }, destroyCancellationToken);
    }

    void OnDestroy()
    {
        if (_session is IWebSocketProtocolSession session)
        {
            session.Dispose();
            _session = null;
        }
    }
}

public sealed class S2C_ClientLoginHandler : ProtocolHandlerBase<S2C_ClientLogin>
{
    IServiceProvider _provider;

    public S2C_ClientLoginHandler(IServiceProvider provider) => _provider = provider;

    protected override ValueTask HandlerAsync(IProtocolSession session, S2C_ClientLogin protocol, CancellationToken cancellationToken)
    {
        Debug.LogWarning(nameof(S2C_ClientLoginHandler));
        if (_provider.GetService(typeof(Main)) is Main main && main != null)
            main.PlayerName = protocol.Name;
        else
            Debug.LogWarning(new { session, protocol, protocol.Name });
        return default;
    }
}

public sealed class S2C_HeartbeatHandler : ProtocolHandlerBase<S2C_Heartbeat>
{
    IServiceProvider _provider;

    public S2C_HeartbeatHandler(IServiceProvider provider) => _provider = provider;

    protected override ValueTask HandlerAsync(IProtocolSession session, S2C_Heartbeat protocol, CancellationToken cancellationToken)
    {
        Debug.LogWarning(nameof(S2C_HeartbeatHandler));
        return session.SendAsync(new C2S_Heartbeat(), cancellationToken);
    }
}
