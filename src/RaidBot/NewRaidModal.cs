using Discord;
using Discord.Interactions;

namespace RaidBot;

public class NewRaidModal : IModal
{
    public string Title => "Create a new raid";

    [InputLabel("Name")]
    [ModalTextInput("newraid_name", TextInputStyle.Short, "New Raid", maxLength: 93)]
    public string Name { get; set; } = string.Empty;

    [InputLabel("Date")]
    [ModalTextInput("newraid_date", TextInputStyle.Short, "Jan 10", maxLength: 93)]
    public string Date { get; set; } = string.Empty;

    [InputLabel("Time")]
    [ModalTextInput("newraid_time", TextInputStyle.Short, "9:00 PM", maxLength: 93)]
    public string Time { get; set; } = string.Empty;
}
