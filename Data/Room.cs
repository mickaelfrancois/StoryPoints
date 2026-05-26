namespace StoryPoints.Data;

public class Room
{
    public Guid Id { get; set; }
    public Scale Scale { get; set; }
    public int? MaxVoteDurationSeconds { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivityUtc { get; set; }
}
