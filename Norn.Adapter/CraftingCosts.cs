namespace Norn.Adapter;

/// <summary>
/// Valheim's crafting-cost math: what a recipe asks for at a given quality
/// level. Feature code, not <c>GameCore</c> — nothing here touches
/// <c>Read</c>/<c>Write</c>.
/// </summary>
public static class CraftingCosts
{
    // game-derived: Piece.Requirement.GetAmount(qualityLevel), confirmed
    // against Valheim 1.0.16. Quality 1 (the craft) costs m_amount; an upgrade
    // to quality q costs m_amountPerLevel scaled by q-1 up to q=3, then by
    // 4 + (q-4)/2 — a jump from 2 to 4 at q=4, faithfully kept. The game's
    // "+ m_amount for an upgrader resource" term is left out: Materials never
    // asks for those.
    public static int AmountFor(RecipeResourceDto resource, int quality)
    {
        if (quality <= 1)
        {
            return resource.Amount;
        }

        var scale = quality < 4 ? quality - 1 : 4f + (quality - 4) / 2f;
        return (int)Math.Floor((double)(scale * resource.AmountPerLevel));
    }

    /// <summary>The first quality level the recipe itself provides: 1 (the
    /// craft), or 2 for a recipe that only upgrades.</summary>
    public static int FirstLevel(RecipeDto recipe) => recipe.Craftable ? 1 : 2;

    /// <summary>
    /// Everything <paramref name="recipe"/> asks for to reach
    /// <paramref name="targetLevel"/>, <paramref name="times"/> over: only the
    /// single step to that level when <paramref name="upgradeOnly"/>, otherwise
    /// every step from <see cref="FirstLevel"/> up to it. Merged by item in the
    /// recipe's own requirement order, zero amounts dropped; empty when
    /// <paramref name="targetLevel"/> is below <see cref="FirstLevel"/>.
    /// </summary>
    public static IReadOnlyList<ItemAmount> Materials(RecipeDto recipe, int targetLevel, bool upgradeOnly, int times)
    {
        var totals = new OrderedDictionary<string, int>();
        var fromLevel = upgradeOnly ? targetLevel : FirstLevel(recipe);

        for (var level = Math.Max(fromLevel, FirstLevel(recipe)); level <= targetLevel; level++)
        {
            // game-derived: InventoryGui/Player requirement filters, Valheim
            // 1.0.16 — a requirement counts only when the station's m_upgrader
            // matches its m_upgraderResource, so a normal station never asks
            // for an upgrader resource.
            foreach (var resource in recipe.Resources.Where(r => !r.Upgrader))
            {
                var amount = AmountFor(resource, level) * times;
                if (amount > 0)
                {
                    totals[resource.ItemName] = totals.GetValueOrDefault(resource.ItemName) + amount;
                }
            }
        }

        return [.. totals.Select(t => new ItemAmount(t.Key, t.Value))];
    }

    // game-derived: Player placing a piece calls ConsumeResources(m_resources, 0),
    // i.e. GetAmount(0) — the plain m_amount, never a per-level amount.
    // Confirmed against Valheim 1.0.16.
    /// <summary>Everything <paramref name="piece"/> asks for to be placed
    /// <paramref name="times"/> times, merged by item in its own order.</summary>
    public static IReadOnlyList<ItemAmount> Materials(PieceDto piece, int times)
    {
        var totals = new OrderedDictionary<string, int>();
        foreach (var (itemName, amount) in piece.Resources)
        {
            if (amount * times > 0)
            {
                totals[itemName] = totals.GetValueOrDefault(itemName) + amount * times;
            }
        }

        return [.. totals.Select(t => new ItemAmount(t.Key, t.Value))];
    }
}
