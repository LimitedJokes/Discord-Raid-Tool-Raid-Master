namespace RaidBot;

public class RaidContent
{
    public RaidContent(string name, DateTimeOffset date, ulong ownerId)
    {
        Name = name;
        Date = date;
        OwnerId = ownerId;
    }

    public string Name { get; set; }

    public DateTimeOffset Date { get; set; }

    public ulong OwnerId { get; set; }

    public List<RaidMember> Members { get; set; } = new();

    public ulong? MessageId { get; set; }
}
