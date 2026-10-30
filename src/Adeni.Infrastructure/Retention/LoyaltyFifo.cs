namespace Adeni.Infrastructure.Retention;

using Adeni.Domain.Retention;

/// <summary>FIFO consumption of earn lots for expiry (Sprint 37.3-C).</summary>
internal static class LoyaltyFifo
{
    private sealed record Lot(int Remaining, DateTimeOffset EarnedAt);

    public static int ExpiredPointsNotYetWritten(
        IReadOnlyList<LoyaltyTransaction> ledger,
        int expiryMonths,
        DateTimeOffset now)
    {
        if (expiryMonths <= 0)
        {
            return 0;
        }

        var lots = new List<Lot>();
        foreach (var row in ledger.OrderBy(x => x.OccurredAt).ThenBy(x => x.Sequence))
        {
            switch (row.Kind)
            {
                case LoyaltyTransactionKind.Earn when row.Points > 0:
                    lots.Add(new Lot(row.Points, row.OccurredAt));
                    break;
                case LoyaltyTransactionKind.Earn when row.Points < 0:
                case LoyaltyTransactionKind.Redeem when row.Points < 0:
                case LoyaltyTransactionKind.Expire:
                    Consume(lots, -row.Points);
                    break;
                case LoyaltyTransactionKind.Redeem when row.Points > 0:
                    lots.Add(new Lot(row.Points, row.OccurredAt));
                    break;
                case LoyaltyTransactionKind.Adjust when row.Points > 0:
                    lots.Add(new Lot(row.Points, row.OccurredAt));
                    break;
                case LoyaltyTransactionKind.Adjust when row.Points < 0:
                    Consume(lots, -row.Points);
                    break;
            }
        }

        var cutoff = now.AddMonths(-expiryMonths);
        return lots.Where(x => x.EarnedAt <= cutoff).Sum(x => x.Remaining);
    }

    private static void Consume(List<Lot> lots, int amount)
    {
        var left = amount;
        for (var i = 0; i < lots.Count && left > 0; i++)
        {
            var lot = lots[i];
            if (lot.Remaining <= 0)
            {
                continue;
            }

            var take = Math.Min(lot.Remaining, left);
            lots[i] = lot with { Remaining = lot.Remaining - take };
            left -= take;
        }
    }
}
