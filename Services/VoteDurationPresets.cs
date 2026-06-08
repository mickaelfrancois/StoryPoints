namespace StoryPoints.Services;

/// <summary>Une option de délai max de vote proposée dans l'UI (valeur en secondes, libellé).</summary>
public sealed record VoteDurationOption(int? Seconds, string Label);

/// <summary>
/// Préréglages partagés du délai max de vote, utilisés à la création (Home) et à
/// l'édition (RoomPage) d'un salon. Source unique pour éviter toute divergence.
/// </summary>
public static class VoteDurationPresets
{
    public static readonly IReadOnlyList<VoteDurationOption> Options = new[]
    {
        new VoteDurationOption(20, "20 secondes"),
        new VoteDurationOption(30, "30 secondes"),
        new VoteDurationOption(40, "40 secondes"),
        new VoteDurationOption(60, "60 secondes"),
        new VoteDurationOption(null, "Illimité"),
    };
}
