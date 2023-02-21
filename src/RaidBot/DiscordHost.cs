using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace RaidBot;

internal class DiscordHost : IHostedService
{
    private readonly DiscordSocketClient _discord;
    private readonly InteractionService _interactions;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventPersistence _eventPersistence;
    private readonly ILogger _logger;
    private readonly DiscordConfigurationOptions _options;
    private CancellationToken _stoppingToken;

    public DiscordHost(DiscordSocketClient discord, IServiceProvider serviceProvider, IEventPersistence eventPersistence, ILogger<DiscordHost> logger, IOptions<DiscordConfigurationOptions> options)
    {
        _discord = discord;
        _interactions = new InteractionService(discord, new()
        {
            AutoServiceScopes = true,
            DefaultRunMode = RunMode.Async,
            ExitOnMissingModalField = true,
            UseCompiledLambda = true
        });
        _serviceProvider = serviceProvider;
        _eventPersistence = eventPersistence;
        _logger = logger;
        _options = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _stoppingToken = cancellationToken;
        _discord.InteractionCreated += HandleInteractionAsync;
        _discord.Ready += OnReady;
        _discord.Log += Log;
        _interactions.Log += Log;
        await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);
        await _discord.LoginAsync(TokenType.Bot, _options.BotToken);
        await _discord.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _discord.InteractionCreated -= HandleInteractionAsync;
        _discord.Ready -= OnReady;
        _discord.Log -= Log;
        _interactions.Log -= Log;
        await _discord.LogoutAsync();
    }

    private async Task MaintainAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var today = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _serviceProvider.GetRequiredService<TimeZoneInfo>()).Date;

                foreach (var guild in await _discord.Rest.GetGuildsAsync(true))
                {
                    var options = await _eventPersistence.LoadAsync<GuildOptions>(guild.Id);

                    if (options is not null)
                    {
                        var channels = await guild.GetChannelsAsync();
                        var eventChannels = new List<(RaidContent?, RestGuildChannel)>();
                        foreach (var channel in channels.Where(c => c is INestedChannel nested && nested.CategoryId == options.CategoryId))
                        {
                            var raidContent = await _eventPersistence.LoadAsync<RaidContent>(channel.Id);

                            if (raidContent is not null && raidContent.Date < DateTimeOffset.UtcNow.AddDays(-2))
                            {
                                await channel.DeleteAsync();
                            }
                            else
                            {
                                if (raidContent is not null)
                                {
                                    string prefix = raidContent.Date.Date == today ? "⭐" :
                                        raidContent.Date.Date < today ? "❌" : "";

                                    string targetName = $"{prefix}{raidContent.Date:MMM-dd}-{raidContent.Name.Replace(' ', '-')}";

                                    if (channel.Name != targetName)
                                    {
                                        await channel.ModifyAsync(c => c.Name = targetName);
                                    }
                                }

                                eventChannels.Add((raidContent, channel));
                            }
                        }

                        int index = (channels.FirstOrDefault(c => c.Id == options.CategoryId)?.Position ?? 0) + 1;
                        foreach ((RaidContent? raidContent, RestGuildChannel channel) in eventChannels.OrderBy(t => t.Item1?.Date).ThenBy(t => t.Item2.Position))
                        {
                            if (channel.Position != index)
                            {
                                int thisIndex = index;
                                await channel.ModifyAsync(c => c.Position = thisIndex);
                            }
                            index++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background maintenance failed");
            }

            await Task.Delay(TimeSpan.FromHours(1), cancellationToken);
        }
    }

    private async Task HandleInteractionAsync(SocketInteraction arg)
    {
        try
        {
            // Create an execution context that matches the generic type parameter of your InteractionModuleBase<T> modules
            var ctx = new SocketInteractionContext(_discord, arg);
            await _interactions.ExecuteCommandAsync(ctx, _serviceProvider);
            var t = ctx.Interaction.GetType();
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to execute", ex);

            // If a Slash Command execution fails it is most likely that the original interaction acknowledgement will persist. It is a good idea to delete the original
            // response, or at least let the user know that something went wrong during the command execution.
            if (arg.Type == InteractionType.ApplicationCommand)
                await arg.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "<Pending>")]
    private Task Log(LogMessage arg)
    {
        _logger.Log(
            arg.Severity switch
            {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Info => LogLevel.Information,
                LogSeverity.Verbose => LogLevel.Trace,
                LogSeverity.Debug => LogLevel.Debug,
                _ => (LogLevel)arg.Severity
            },
            exception: arg.Exception,
            message: arg.Message);
        return Task.CompletedTask;
    }

    private async Task OnReady()
    {
        if (_options.ServerId > 0)
        {
            await _interactions.RegisterCommandsToGuildAsync(_options.ServerId);
        }
        else
        {
            await _interactions.RegisterCommandsGloballyAsync();
        }
        _ = MaintainAsync(_stoppingToken);
    }
}
