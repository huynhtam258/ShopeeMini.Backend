using ShopeeMini.Domain.Entities;

namespace ShopeeMini.Application.Common;

public static class DiscountCalculator
{
    /// <summary>The single best active discount percent for a SKU at the given instant, or 0 if none applies.</summary>
    public static decimal GetActiveDiscountPercent(IEnumerable<ProductDiscount> discounts, DateTime atUtc)
    {
        var active = discounts
            .Where(d => d.IsActive && d.StartAt <= atUtc && d.EndAt >= atUtc)
            .Select(d => d.DiscountPercent)
            .DefaultIfEmpty(0m)
            .Max();

        return active;
    }

    public static decimal ApplyDiscount(decimal price, decimal discountPercent)
    {
        var effective = price * (1 - discountPercent / 100m);
        return Math.Round(effective, 2, MidpointRounding.AwayFromZero);
    }
}
