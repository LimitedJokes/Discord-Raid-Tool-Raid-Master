using Discord.Interactions;

namespace RaidBot;

public class JoinModal : IModal
{
    public string Title => "Join Raid";

    [ModalTextInput("raidjoin_class")]
    public string Class { get; set; } = string.Empty;

    [ModalTextInput("raidjoin_role")]
    public string Role { get; set; } = string.Empty;

    [ModalTextInput("raidjoin_name")]
    public string Name { get; set; } = string.Empty;
}
