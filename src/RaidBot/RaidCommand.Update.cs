using Discord;
using Discord.Interactions;

namespace RaidBot;

public partial class RaidCommand
{
    [SlashCommand("update", "Updates a raid signup. This command can only be used by the creator of a raid.")]
    public async Task UpdateAsync(
        [Summary("name", "The name of the raid. This value should only contain letters, numbers, and spaces.")] string? name = null,
        [Summary("date", "The date of the raid. This value should use the server time.")] string? dateString = null,
        [Summary("time", "The time of the raid. This value should use the server time.")] string? timeString = null)
    {
        await QueueContentTaskAsync(async raidContent =>
        {
            if (Context.User.Id == raidContent.OwnerId)
            {
                var options = await _persistence.LoadAsync<GuildOptions>(Context.Guild.Id);

                if (options is null)
                {
                    await RespondSilentAsync("The guild has not been configured yet.");
                    return;
                }

                if (!((IGuildUser)Context.User).RoleIds.Contains(options.CreateRoleId))
                {
                    await RespondSilentAsync("You do not have permission to use this command.");
                    return;
                }

                if (name?.Length > 0)
                {
                    if (name.Length > 93 || !name.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or ' '))
                    {
                        await RespondSilentAsync("The name parameter is not valid. Names can only contain letters, numbers, and spaces.");
                        return;
                    }

                    raidContent.Name = name;
                }

                DateOnly? date = null;
                TimeOnly? time = null;

                if (dateString?.Length > 0)
                {
                    if (!DateOnly.TryParse(dateString, out var parsedDate))
                    {
                        await RespondSilentAsync("The date parameter is not a valid date.");
                        return;
                    }
                    else
                    {
                        date = parsedDate;
                    }
                }

                if (timeString?.Length > 0)
                {
                    if (!TimeOnly.TryParse(timeString, out var parsedTime))
                    {
                        await RespondSilentAsync("The time parameter is not a valid time of day.");
                        return;
                    }
                    else
                    {
                        time = parsedTime;
                    }
                }

                if (date.HasValue || time.HasValue)
                {
                    DateTime dateTime;
                    if (date.HasValue)
                    {
                        if (time.HasValue)
                        {
                            dateTime = date.Value.ToDateTime(time.Value);
                        }
                        else
                        {
                            dateTime = date.Value.ToDateTime(new TimeOnly(raidContent.Date.TimeOfDay.Ticks));
                        }
                    }
                    else if (time.HasValue)
                    {
                        dateTime = DateOnly.FromDateTime(raidContent.Date.LocalDateTime).ToDateTime(time.Value);
                    }
                    else
                    {
                        dateTime = default;
                    }
                    raidContent.Date = new DateTimeOffset(dateTime, _timeZone.GetUtcOffset(dateTime));

                    if (raidContent.Date < DateTimeOffset.UtcNow)
                    {
                        await RespondSilentAsync("The date and time is in the past!");
                        return;
                    }
                }

                bool isToday = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _timeZone).Date == raidContent.Date.Date;

                await SaveAsync(Context.Channel, raidContent);
                await ((ITextChannel)Context.Channel).ModifyAsync(channel => channel.Name = $"{(isToday ? "⭐" : "")}{raidContent.Date:MMM-dd}-{raidContent.Name.Replace(' ', '-')}");
                await RespondSilentAsync("Raid updated.");
            }
            else
            {
                await RespondSilentAsync("You are not the owner of this raid channel.");
            }
        });
    }
}