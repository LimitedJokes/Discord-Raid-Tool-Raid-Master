using Discord.WebSocket;
using RaidBot;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DiscordConfigurationOptions>(builder.Configuration.GetSection("Discord"));
builder.Services.AddSingleton(TimeZoneInfo.FindSystemTimeZoneById(builder.Configuration["TimeZone"]!));
builder.Services.AddSingleton<IEventPersistence>(_ => new LocalPersistence(builder.Configuration["PersistantFolder"]!));
//builder.Services.AddSingleton<IEventPersistence>(_ => new AzurePersistence());
builder.Services.AddSingleton(new CommandQueue());
builder.Services.AddSingleton(_ => new DiscordSocketClient());
builder.Services.AddHostedService<DiscordHost>();
builder.Services.AddHostedService<CommandProcessor>();

var app = builder.Build();

app.MapGet("/", () => $"Response created at {DateTimeOffset.UtcNow}");
app.MapGet("/ping", () => "pong");

app.Run();
