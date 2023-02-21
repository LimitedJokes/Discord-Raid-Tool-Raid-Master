using Discord;
using Discord.Interactions;

namespace RaidBot;

public partial class RaidCommand
{
    [SlashCommand("add", "Adds a member to a raid signup. This command can only be used by the creator of a raid.")]
    public async Task AddAsync(
        [Summary("user", "The discord user of this character.")] IUser user,
        [Summary("class", "The class of the character.")] PlayerClass playerClass,
        [Summary("role", "The role they will be performing.")] PlayerRole playerRole,
        [Summary("name", "The optional character name.")] string? name = null)
    {
        await QueueContentTaskAsync(raidContent => Context.User.Id == raidContent.OwnerId ?
            AddInternalAsync(raidContent, playerClass, playerRole, user, name) :
            RespondSilentAsync("You are not the owner of this raid channel."));
    }

    [SlashCommand("join", "Joins a raid signup.")]
    public async Task JoinAsync(
        [Summary("class", "The class of your character.")] PlayerClass playerClass,
        [Summary("role", "The role you will be performing.")] PlayerRole playerRole,
        [Summary("name", "The optional character name.")] string? name = null)
    {
        await QueueContentTaskAsync(raidContent => AddInternalAsync(raidContent, playerClass, playerRole, Context.User, name));
    }

    [ComponentInteraction("raidjoin", ignoreGroupNames: true)]
    public Task JoinClick2Async()
    {
        _commandQueue.Queue(async () =>
        {
            if (await ReadContentAsync() is { } raidContent)
            {
                var current = raidContent.Members.Find(m => m.OwnerId == Context.User.Id);

                await RespondWithModalAsync(new ModalBuilder()
                    .WithTitle("Join Raid")
                    .WithCustomId("raidjoin_respond")
                    .AddTextInput("Role", "raidjoin_role", placeholder: "tank, healer, ranged, melee", maxLength: 10, required: true, value: current?.PlayerRole.ToString())
                    .AddTextInput("Class", "raidjoin_class", placeholder: "druid, hunter, mage, etc...", maxLength: 12, required: true, value: current?.PlayerClass.ToString())
                    .AddTextInput("Character Name (optional)", "raidjoin_name", maxLength: 12, required: false, value: current?.Name)
                    .Build());
            }
            else
            {
                await RespondSilentAsync("This channel is not a raid channel.");
            }
        });
        return Task.CompletedTask;
    }

    [ModalInteraction("raidjoin_respond", ignoreGroupNames: true)]
    public async Task JoinRespondAsync(JoinModal modal)
    {
        string? name = modal.Name;
        PlayerClass? playerClass = TryParseClass(modal.Class);
        PlayerRole? playerRole = TryParseRole(modal.Role);

        if (!playerClass.HasValue)
        {
            await RespondSilentAsync($"{Context.User.Mention} Class is not valid.");
            return;
        }

        if (!playerRole.HasValue)
        {
            await RespondSilentAsync($"{Context.User.Mention} Role is not valid.");
            return;
        }

        await QueueContentTaskAsync(raidContent => AddInternalAsync(raidContent, playerClass.Value, playerRole.Value, Context.User, name));
    }

    [ComponentInteraction("raidjoin:*", ignoreGroupNames: true)]
    public async Task JoinClickAsync(string roleString)
    {
        var playerRole = Enum.Parse<PlayerRole>(roleString);

        await QueueContentTaskAsync(_ => RespondSilentAsync(
            "Select Your Class",
            new ComponentBuilder()
                .WithSelectMenu(
                    $"raidjoin_class:{playerRole}",
                    Enum.GetValues<PlayerClass>().Select(c => new SelectMenuOptionBuilder().WithLabel(c.ToString()).WithValue(c.ToString()).WithEmote(GetEmote(c))).ToList(),
                    "Select your class")
                .Build()));
    }

    [ComponentInteraction("raidjoin_class:*", ignoreGroupNames: true)]
    public async Task JoinRespondAsync(string roleString, string[] selection)
    {
        var playerRole = Enum.Parse<PlayerRole>(roleString);
        var playerClass = Enum.Parse<PlayerClass>(selection[0]);

        await QueueContentTaskAsync(raidContent => AddInternalAsync(raidContent, playerClass, playerRole, Context.User, null));
    }

    private async Task AddInternalAsync(
        RaidContent raidContent,
        PlayerClass playerClass,
        PlayerRole playerRole,
        IUser user,
        string? name)
    {
        var existing = raidContent.Members.Find(m => m.OwnerId == user.Id);

        if (existing is not null)
        {
            existing.PlayerClass = playerClass;
            existing.PlayerRole = playerRole;
            existing.Name = name;
            existing.OwnerName = (user as IGuildUser)?.DisplayName ?? user.Username;
            raidContent.Members.RemoveAll(m => m != existing && m.OwnerId == user.Id);
        }
        else
        {
            raidContent.Members.Add(new()
            {
                Name = name,
                OwnerId = user.Id,
                OwnerName = (user as IGuildUser)?.DisplayName ?? user.Username,
                PlayerClass = playerClass,
                PlayerRole = playerRole
            });
        }

        await SaveAsync(Context.Channel, raidContent);

        string message = $"{user.Mention} {(existing is not null ? "updated." : "added.")}";

        if (Context.Interaction.Type != InteractionType.MessageComponent)
        {
            await RespondSilentAsync(message);
        }
    }

    private static PlayerClass? TryParseClass(string? input)
    {
        if (input?.Length > 0 && _classLookup.TryGetValue(input.Trim(), out var playerClass))
        {
            return playerClass;
        }
        return null;
    }

    private static PlayerRole? TryParseRole(string? input)
    {
        if (input?.Length > 0 && _roleLookup.TryGetValue(input.Trim(), out var playerRole))
        {
            return playerRole;
        }
        return null;
    }

    private static readonly IReadOnlyDictionary<string, PlayerClass> _classLookup = BuildClassLookup();

    private static readonly IReadOnlyDictionary<string, PlayerRole> _roleLookup = BuildRoleLookup();

    private static Dictionary<string, PlayerClass> BuildClassLookup()
    {
        var lookup = new Dictionary<string, PlayerClass>(StringComparer.CurrentCultureIgnoreCase);

        foreach (var value in Enum.GetValues<PlayerClass>())
        {
            lookup[value.ToString()] = value;
        }

        lookup["huntard"] = PlayerClass.Hunter;
        lookup["pally"] = lookup["pala"] = PlayerClass.Paladin;
        lookup["rouge"] = PlayerClass.Rogue;
        lookup["shammy"] = lookup["sham"] = PlayerClass.Shaman;
        lookup["lock"] = PlayerClass.Warlock;
        lookup["warr"] = PlayerClass.Warrior;
        lookup["dk"] = lookup["death knight"] = PlayerClass.DeathKnight;

        return lookup;
    }

    private static Dictionary<string, PlayerRole> BuildRoleLookup()
    {
        var lookup = new Dictionary<string, PlayerRole>(StringComparer.CurrentCultureIgnoreCase);

        foreach (var value in Enum.GetValues<PlayerRole>())
        {
            lookup[value.ToString()] = value;
        }

        lookup["caster"] = lookup["range"] = PlayerRole.Ranged;
        lookup["heals"] = lookup["heal"] = PlayerRole.Healer;

        return lookup;
    }
}
