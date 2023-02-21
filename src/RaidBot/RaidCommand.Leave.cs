using Discord.Interactions;

namespace RaidBot;

public partial class RaidCommand
{
    [SlashCommand("leave", "Leaves a raid signup.")]
    public Task SlashLeaveAsync() => LeaveAsync();

    [ComponentInteraction("raidleave", ignoreGroupNames: true)]
    public Task ClickLeaveAsync() => LeaveAsync();

    private async Task LeaveAsync()
    {
        await QueueContentTaskAsync(async raidContent =>
        {
            if (raidContent.Members.RemoveAll(m => m.OwnerId == Context.User.Id) > 0)
            {
                await SaveAsync(Context.Channel, raidContent);
                await RespondSilentAsync("You've left the raid.");
            }
            else
            {
                await RespondSilentAsync("You aren't in this raid.");
            }
        });
    }
}
