using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using System.Text;

namespace RaidBot;

[Group("raid", "Commands for manipulating raid signups.")]
public partial class RaidCommand : InteractionModuleBase
{
    private readonly IEventPersistence _persistence;
    private readonly TimeZoneInfo _timeZone;
    private readonly CommandQueue _commandQueue;
    private readonly DiscordConfigurationOptions _options;

    public RaidCommand(IEventPersistence persistence, IOptions<DiscordConfigurationOptions> options, TimeZoneInfo timeZone, CommandQueue commandQueue)
    {
        _persistence = persistence;
        _options = options.Value;
        _timeZone = timeZone;
        _commandQueue = commandQueue;
    }

    private Emote? GetEmote(Enum en) => GetEmote(en.ToString());

    private Emote? GetEmote(string key)
    {
        if (Emote.TryParse(FindRawEmote(key), out var emote))
        {
            return emote;
        }
        return null;
    }

    private string? FindRawEmote(Enum key) => FindRawEmote(key.ToString());

    private string? FindRawEmote(string key)
    {
        if (_options.Emoji.TryGetValue(key, out var rawValue))
        {
            return rawValue;
        }
        return null;
    }

    private async Task QueueTaskAsync(Func<Task> task)
    {
        await Context.Interaction.DeferAsync(true);
        _commandQueue.Queue(async () =>
        {
            try
            {
                await task();
            }
            catch
            {
                await RespondSilentAsync(":x: Something went wrong...");
                throw;
            }
        });
    }

    private Task QueueContentTaskAsync(Func<RaidContent, Task> task)
    {
        return QueueTaskAsync(async () =>
        {
            if (await ReadContentAsync() is { } raidContent)
            {
                await task(raidContent);
            }
            else
            {
                await RespondSilentAsync("This channel is not a raid channel.");
            }
        });
    }

    private async Task<RaidContent?> ReadContentAsync()
    {
        return await _persistence.LoadAsync<RaidContent>(Context.Channel.Id);
    }

    private async Task<IUserMessage?> GetDeclarationAsync()
    {
        if (Context.Interaction is SocketMessageComponent msg && msg.Message.Author.Id == Context.Client.CurrentUser.Id && msg.Message.IsPinned)
        {
            return msg.Message;
        }
        else
        {
            var pinned = await Context.Channel.GetPinnedMessagesAsync();
            if (pinned.FirstOrDefault(msg => msg.Author.Id == Context.Client.CurrentUser.Id) is IUserMessage declarationMessage)
            {
                return declarationMessage;
            }
        }

        return null;
    }

    private async Task RespondSilentAsync(string message, MessageComponent? components = null)
    {
        if (Context.Interaction.HasResponded)
        {
            await Context.Interaction.FollowupAsync(message, ephemeral: true, components: components);
        }
        else
        {
            await Context.Interaction.RespondAsync(message, ephemeral: true, components: components);
        }
    }

    private async Task SaveAsync(IMessageChannel channel, RaidContent raidContent)
    {
        foreach (var member in raidContent.Members)
        {
            if (string.IsNullOrEmpty(member.OwnerName))
            {
                var user = await Context.Guild.GetUserAsync(member.OwnerId);

                if (user is not null)
                {
                    member.OwnerName = user.DisplayName;
                }
            }
        }

        if (!raidContent.MessageId.HasValue)
        {
            var declarationMessage = await GetDeclarationAsync();

            if (declarationMessage is null)
            {
                await RespondSilentAsync("This channel is not a raid channel.");
                return;
            }

            raidContent.MessageId = declarationMessage.Id;
        }

        if (raidContent.MessageId > 0)
        {
            await _persistence.SaveAsync(channel.Id, raidContent);
            await channel.ModifyMessageAsync(raidContent.MessageId.Value, message =>
            {
                message.Content = MakeMessageContent(raidContent);
                message.Embed = MakeMessageEmbed(raidContent);
                message.Components = MakeMessageComponents();
                message.AllowedMentions = new AllowedMentions { UserIds = new() { Context.User.Id } };
            });
        }
        else
        {
            var message = await channel.SendMessageAsync(
                MakeMessageContent(raidContent),
                embed: MakeMessageEmbed(raidContent),
                allowedMentions: new AllowedMentions { UserIds = new() { Context.User.Id } },
                components: MakeMessageComponents());
            await message.PinAsync();
            raidContent.MessageId = message.Id;
            await _persistence.SaveAsync(channel.Id, raidContent);
        }
    }

    private Embed MakeMessageEmbed(RaidContent raidContent)
    {
        var builder = new EmbedBuilder()
            .WithTitle(raidContent.Name)
            .AddField("Date (Server Time)", raidContent.Date.ToString("dddd, MMMM dd, yyyy hh:mm tt"), inline: true)
            .AddField("Date (Local Time)", $"<t:{raidContent.Date.ToUnixTimeSeconds()}:F>", inline: true)
            .AddField("Total Signups", raidContent.Members.Count.ToString("N0"), inline: false);

        var sb = new StringBuilder();

        AddField(builder, sb, raidContent, "Tanks", PlayerRole.Tank);
        AddField(builder, sb, raidContent, "Healers", PlayerRole.Healer);
        AddField(builder, sb, raidContent, "Melee DPS", PlayerRole.Melee);
        AddField(builder, sb, raidContent, "Ranged DPS", PlayerRole.Ranged);

        return builder.Build();
    }

    private static string MakeMessageContent(RaidContent raidContent)
    {
        return $@"<@!{raidContent.OwnerId}>, your raid will automatically delete 48 hours after the start time.
You can manually add `/raid add @user` or remove `/raid kick @user` users.
You can change this event with `/raid update`.";
    }

    private static MessageComponent MakeMessageComponents()
    {
        return new ComponentBuilder()
            .WithButton("Join or Update", "raidjoin", ButtonStyle.Primary)
            .WithButton("Leave", "raidleave", ButtonStyle.Danger)
            .Build();
    }

    private void AddField(EmbedBuilder builder, StringBuilder sb, RaidContent raidContent, string name, PlayerRole role)
    {
        var fieldName = $"{FindRawEmote(role)} {name} ({raidContent.Members.Count(m => m.PlayerRole == role)})";
        var roleMembers = raidContent.Members.Where(member => member.PlayerRole == role);

        if (!roleMembers.Any())
        {
            builder.AddField(fieldName, "none", inline: true);
            return;
        }

        bool first = true;
        sb.Length = 0;

        foreach (var member in roleMembers)
        {
            if (!first)
            {
                sb.Append('\n');
            }

            sb.Append('`')
                .Append(raidContent.Members.IndexOf(member) + 1)
                .Append("` ")
                .Append(FindRawEmote(member.PlayerClass))
                .Append(' ');

            if (member.Name?.Length > 0)
            {
                sb.Append("**").Append(char.ToUpper(member.Name[0]));

                for (int i = 1; i < member.Name.Length; i++)
                {
                    sb.Append(char.ToLower(member.Name[i]));
                }

                sb.Append("**");

                if (member.OwnerName?.Length > 0)
                {
                    if (!member.OwnerName.Contains(member.Name, StringComparison.CurrentCultureIgnoreCase))
                    {
                        sb.Append(" (").AppendTruncated(member.OwnerName, 15).Append(')');
                    }
                }
                else
                {
                    sb.Append(" (<@!").Append(member.OwnerId).Append(">)");
                }
            }
            else if (member.OwnerName?.Length > 0)
            {
                sb.Append("**").AppendTruncated(member.OwnerName, 15).Append("**");
            }
            else
            {
                sb.Append("<@!").Append(member.OwnerId).Append('>');
            }

            first = false;
        }

        builder.AddField(fieldName, sb.ToString(), inline: true);
    }

    private readonly OverwritePermissions _everyonePermissions = new(
        addReactions: PermValue.Allow,
        viewChannel: PermValue.Allow,
        sendMessages: PermValue.Allow,
        embedLinks: PermValue.Allow,
        attachFiles: PermValue.Allow,
        readMessageHistory: PermValue.Allow,
        useExternalEmojis: PermValue.Allow,
        useSlashCommands: PermValue.Allow,
        useApplicationCommands: PermValue.Allow,
        useExternalStickers: PermValue.Allow);

    private readonly OverwritePermissions _everyoneHiddenPermissions = new(viewChannel: PermValue.Deny);

    private readonly OverwritePermissions _ownerPermissions = new(
        addReactions: PermValue.Allow,
        viewChannel: PermValue.Allow,
        sendMessages: PermValue.Allow,
        manageMessages: PermValue.Allow,
        attachFiles: PermValue.Allow,
        readMessageHistory: PermValue.Allow,
        useExternalEmojis: PermValue.Allow,
        useSlashCommands: PermValue.Allow,
        useApplicationCommands: PermValue.Allow,
        useExternalStickers: PermValue.Allow);
}
