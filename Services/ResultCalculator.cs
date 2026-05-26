using System.Globalization;

namespace StoryPoints.Services;

public record VoteResults(
    string? Min,
    string? Max,
    string? Median,
    int NumericVoteCount,
    IReadOnlyList<CardCount> SpecialBreakdown,
    IReadOnlyList<MemberVote> AllVotes);

public record CardCount(string Card, int Count);

public record MemberVote(string MemberName, string Value);

public static class ResultCalculator
{
    public static VoteResults Compute(
        IReadOnlyList<MemberVote> votes,
        IReadOnlyList<string> scaleCards)
    {
        var numericCards = scaleCards
            .Select(c => (Card: c, Number: TryParse(c)))
            .Where(t => t.Number is not null)
            .Select(t => (t.Card, Number: t.Number!.Value))
            .OrderBy(t => t.Number)
            .ToList();

        var numericVotes = votes
            .Select(v => (v.MemberName, v.Value, Number: TryParse(v.Value)))
            .Where(t => t.Number is not null)
            .Select(t => (t.MemberName, t.Value, Number: t.Number!.Value))
            .OrderBy(t => t.Number)
            .ToList();

        string? min = null;
        string? max = null;
        string? median = null;

        if (numericVotes.Count > 0)
        {
            min = numericVotes.First().Value;
            max = numericVotes.Last().Value;

            double medianValue = numericVotes.Count % 2 == 1
                ? numericVotes[numericVotes.Count / 2].Number
                : (numericVotes[numericVotes.Count / 2 - 1].Number + numericVotes[numericVotes.Count / 2].Number) / 2.0;

            if (numericCards.Count > 0)
            {
                median = numericCards
                    .OrderBy(c => Math.Abs(c.Number - medianValue))
                    .ThenByDescending(c => c.Number)
                    .First()
                    .Card;
            }
        }

        var specialBreakdown = votes
            .Where(v => TryParse(v.Value) is null)
            .GroupBy(v => v.Value)
            .Select(g => new CardCount(g.Key, g.Count()))
            .OrderBy(c => c.Card)
            .ToList();

        return new VoteResults(min, max, median, numericVotes.Count, specialBreakdown, votes);
    }

    private static double? TryParse(string value) =>
        double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
}
