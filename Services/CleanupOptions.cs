namespace StoryPoints.Services;

public class CleanupOptions
{
    public const string SectionName = "Cleanup";

    public int InactiveDays { get; set; } = 60;
    public int RunIntervalHours { get; set; } = 6;
}
