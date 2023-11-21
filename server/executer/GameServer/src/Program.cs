using GameServer;

var builder = WebApplication.CreateBuilder(args);

builder.Logging
    .ClearProviders()
    .AddConfiguration(builder.Configuration.GetSection("Logging"))
    .AddSimpleConsole()
    .AddDebug();

builder.Host
    .UseOrleans(siloBuilder =>
    {
        siloBuilder.UseLocalhostClustering();
    });

builder.Services
    .AddProtocolHandler();

var app = builder.Build();

app.UseWebSockets(new()
{
    KeepAliveInterval = TimeSpan.FromMinutes(10)
});

app.Use(WebSocketProtocolSession.HandleWebSocketAsync);

await app.RunAsync();
