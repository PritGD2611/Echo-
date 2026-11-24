using UnityEngine;

public enum CursedCardType
{
    Jester,
    King,
    Queen
}

public enum CursedCardOutcome
{
    Undefined,
    NoAnomaly,
    OneAnomaly,
    TwoAnomalies,
    UnknownQuantity,
    TwoJestersAndQueenChance,
    JesterAndTwoQueensChance
}

public static class CursedCardRules
{
    public static CursedCardType Draw()
    {
        return (CursedCardType)Random.Range(0, 3);
    }

    public static CursedCardOutcome Evaluate(CursedCardType first, CursedCardType second, CursedCardType third)
    {
        int Count(CursedCardType type)
        {
            return (first == type ? 1 : 0) + (second == type ? 1 : 0) + (third == type ? 1 : 0);
        }

        return (Count(CursedCardType.Jester), Count(CursedCardType.King), Count(CursedCardType.Queen)) switch
        {
            (2, 1, 0) => CursedCardOutcome.TwoAnomalies,
            (1, 2, 0) => CursedCardOutcome.OneAnomaly,
            (2, 0, 1) => CursedCardOutcome.TwoJestersAndQueenChance,
            (1, 0, 2) => CursedCardOutcome.JesterAndTwoQueensChance,
            (3, 0, 0) => CursedCardOutcome.UnknownQuantity,
            (0, 3, 0) or (0, 0, 3) => CursedCardOutcome.NoAnomaly,
            _ => CursedCardOutcome.Undefined
        };
    }
}
