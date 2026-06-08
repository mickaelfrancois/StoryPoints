namespace StoryPoints.Data;

public class Room
{
    public Guid Id { get; set; }
    /// <summary>Nom libre du salon. Null/vide ⇒ on affiche l'identifiant (Guid) par défaut.</summary>
    public string? Name { get; set; }
    public Scale Scale { get; set; }
    public int? MaxVoteDurationSeconds { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivityUtc { get; set; }
}
