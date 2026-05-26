namespace StoryPoints.Services;

public class RoomLimitOptions
{
    public const string SectionName = "RoomLimits";

    public int MaxTotalRooms { get; set; } = 10_000;
    public int MaxCreationsPerHourPerIp { get; set; } = 20;
}
