using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameCore;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;
using WebSocketProtocolSession;

public sealed class Main : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod]
    static void OnLoadMethod() => DontDestroyOnLoad(new GameObject(nameof(Main), typeof(Main)));

    IServiceProvider _services;
    string _url = "ws://127.0.0.1:8763";
    string _account = "overing";
    bool _guiInitialized;

    public string PlayerName { get; set; }

    void Awake()
    {
        _services = new ServiceCollection()
            .AddSingleton(this)
            .AddSingleton(p =>
            {
                var url = _url;
                var dispatcher = p.GetRequiredService<IProtocolHandlerDispatcher<IWebSocketProtocolSession>>();
                IWebSocketProtocolSession session;
                if (Application.isEditor || Application.platform != RuntimePlatform.WebGLPlayer)
                    session = new WebSocketSharpProtocolSession(url, dispatcher);
                else
                    session = new JsWebSocketProtocolSession(url, dispatcher);
                return session;
            })
            .AddProtocolHandler<IWebSocketProtocolSession>()
            .AddSingleton<IProtocolHandlerDispatcher<IWebSocketProtocolSession>, DefaultProtocolHandlerDispatcher<IWebSocketProtocolSession>>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
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

        var session = _services.GetRequiredService<IWebSocketProtocolSession>();
        if (!session.IsConnected)
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
        var session = _services.GetRequiredService<IWebSocketProtocolSession>();
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
        await session.SendAsync(new C2S_ClientLogin { Account = account }, destroyCancellationToken);
    }

    void OnDestroy()
    {
        if (_services is IDisposable disposable)
        {
            disposable.Dispose();
            _services = null;
        }
    }
}

public sealed class S2C_ClientLoginHandler : ProtocolHandlerBase<IWebSocketProtocolSession, S2C_ClientLogin>
{
    IServiceProvider _provider;

    public S2C_ClientLoginHandler(IServiceProvider provider) => _provider = provider;

    protected override ValueTask HandlerAsync(IWebSocketProtocolSession session, S2C_ClientLogin protocol, CancellationToken cancellationToken)
    {
        Debug.LogWarning(nameof(S2C_ClientLoginHandler));
        if (_provider.GetService(typeof(Main)) is Main main && main != null)
            main.PlayerName = protocol.Name;
        else
            Debug.LogWarning(new { session, protocol, protocol.Name });
        return default;
    }
}

public sealed class S2C_HeartbeatHandler : ProtocolHandlerBase<IWebSocketProtocolSession, S2C_Heartbeat>
{
    IServiceProvider _provider;

    public S2C_HeartbeatHandler(IServiceProvider provider) => _provider = provider;

    protected override ValueTask HandlerAsync(IWebSocketProtocolSession session, S2C_Heartbeat protocol, CancellationToken cancellationToken)
    {
        Debug.LogWarning(nameof(S2C_HeartbeatHandler));
        return session.SendAsync(new C2S_Heartbeat(), cancellationToken);
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProtocolHandler<TSession>(this IServiceCollection collection)
        where TSession : IProtocolSession
    {
        var baseType = typeof(IProtocolHandler<TSession>);
        var query = typeof(Main).Assembly.GetTypes()
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
