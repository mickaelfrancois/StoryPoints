namespace StoryPoints.Data;

public enum Scale
{
    Fibonacci = 0,
    FibonacciShort = 1,
    TShirt = 2
}

public static class ScaleProvider
{
    private static readonly IReadOnlyList<string> Fibonacci =
        new[] { "1", "2", "3", "5", "8", "13", "21", "?", "☕" };

    private static readonly IReadOnlyList<string> FibonacciShort =
        new[] { "1", "2", "3", "5", "8", "13" };

    private static readonly IReadOnlyList<string> TShirt =
        new[] { "XS", "S", "M", "L", "XL", "XXL" };

    public static IReadOnlyList<string> Cards(Scale scale) => scale switch
    {
        Scale.Fibonacci => Fibonacci,
        Scale.FibonacciShort => FibonacciShort,
        Scale.TShirt => TShirt,
        _ => Fibonacci
    };

    public static string Label(Scale scale) => scale switch
    {
        Scale.Fibonacci => "Fibonacci",
        Scale.FibonacciShort => "Fibonacci court",
        Scale.TShirt => "T-shirt",
        _ => scale.ToString()
    };
}
