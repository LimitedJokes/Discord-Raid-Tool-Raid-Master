namespace RaidBot;

internal class CommandProcessor : BackgroundService
{
    private readonly CommandQueue _commandQueue;
    private readonly ILogger _logger;

    public CommandProcessor(CommandQueue commandQueue, ILogger<CommandProcessor> logger)
    {
        _commandQueue = commandQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var command = await _commandQueue.DequeueAsync(stoppingToken);

            if (command is not null)
            {
                try
                {
                    await command();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Executing a command failed.");
                }
            }
        }
    }
}
