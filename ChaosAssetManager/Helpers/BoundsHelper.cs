namespace ChaosAssetManager.Helpers;

public class BoundsHelper
{
    public static (int, int) FindMostSquarePair(List<(int, int)> pairs)
    {
        if ((pairs == null) || (pairs.Count == 0))
            throw new ArgumentException("The list of pairs cannot be null or empty.");

        // Find the pair with the smallest absolute difference
        return pairs.MinBy(pair => Math.Abs(pair.Item1 - pair.Item2));
    }

    public static List<(int, int)> GetFactorPairs(int number)
    {
        var factorPairs = new List<(int, int)>();

        for (var i = 1; i <= Math.Sqrt(number); i++)
            if ((number % i) == 0)
            {
                factorPairs.Add((i, number / i));

                if (i != (number / i))
                    factorPairs.Add((number / i, i));
            }

        return factorPairs;
    }

    public static IEnumerable<(int, int)> OrderByMostSquare(IEnumerable<(int, int)> pairs)
        => pairs.OrderBy(pair => Math.Abs(pair.Item1 - pair.Item2));
}