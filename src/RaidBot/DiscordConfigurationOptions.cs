using Discord.WebSocket;

namespace RaidBot;

public class DiscordConfigurationOptions
{
    public string BotToken { get; set; } = string.Empty;

    public ulong ServerId { get; set; }

    public Dictionary<string, string> Emoji { get; set; } = new();
}
