namespace RaidBot;

public class RaidMember
{
    public PlayerClass PlayerClass { get; set; }

    public PlayerRole PlayerRole { get; set; }

    public ulong OwnerId { get; set; }

    public string? OwnerName { get; set; }

    public string? Name { get; set; }
}
