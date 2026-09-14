namespace Cargo.Domain;

/// <summary>
/// One bay of a vessel, resolved from the stow plan: how much has come off, what is left,
/// and the box that is next on the hook.
/// </summary>
public sealed class BayView
{
    public const int Columns = 6;
    public const int Rows = 4;
    public const int Capacity = Columns * Rows;

    private BayView()
    {
    }

    public required int Index { get; init; }
    public required string Number { get; init; }
    public required int Unloaded { get; init; }
    public required int Remaining { get; init; }
    public required IReadOnlyList<(int Column, int Row, CargoClass Class, bool Ashore)> Cells { get; init; }
    public required IReadOnlyList<(CargoClass Class, int Count, int Percent)> Composition { get; init; }
    public required string ContainerId { get; init; }
    public required string ContainerType { get; init; }
    public required CargoClass ContainerClass { get; init; }
    public required string Weight { get; init; }
    public required string Status { get; init; }
    public required string NextMove { get; init; }
    public required string NextEta { get; init; }

    public static BayView For(Vessel vessel, int bay)
    {
        // Tiers already ashore in this bay, mapped onto the 24-slot cross-section
        var tiersGone = 0;
        var totalCells = vessel.Bays * 5;
        var discharged = (int)Math.Round(totalCells * vessel.UnloadPercent / 100d);
        for (var tier = 0; tier < 5; tier++)
        {
            if (bay * 5 + (4 - tier) < discharged)
            {
                tiersGone++;
            }
        }

        var unloaded = (int)Math.Round(Capacity * tiersGone / 5d);
        var cells = new List<(int, int, CargoClass, bool)>();
        var counts = new Dictionary<CargoClass, int>();
        CargoClass? first = null;

        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var index = row * Columns + column;
                var ashore = index < unloaded;
                var cargo = vessel.Mix.ClassAt(bay * 29 + column * 4 + row * 7);

                if (!ashore)
                {
                    counts[cargo] = counts.GetValueOrDefault(cargo) + 1;
                    first ??= cargo;
                }

                cells.Add((column, row, cargo, ashore));
            }
        }

        var remaining = Capacity - unloaded;
        var composition = counts
            .Select(pair => (pair.Key, pair.Value, remaining == 0 ? 0 : (int)Math.Round(pair.Value * 100d / remaining)))
            .OrderByDescending(entry => entry.Value)
            .ToList();

        var featured = first ?? CargoClass.Empty;
        var prefix = vessel.Operator switch
        {
            "MSC" => "MSCU",
            "Hapag-Lloyd" => "HLCU",
            "ONE" => "ONEU",
            "Maersk" => "MAEU",
            "CMA CGM" => "CMAU",
            _ => "TGHU"
        };

        var seed = bay * 17 + vessel.Length;
        var (typeName, isoCode) = featured switch
        {
            CargoClass.Standard => ("40′ Standard Dry", "22G1"),
            CargoClass.Reefer => ("40′ Reefer", "45R1"),
            CargoClass.Hazard => ("20′ Hazmat · IMDG 3", "22G1"),
            CargoClass.Oversize => ("40′ Open Top", "42U1"),
            _ => ("40′ Empty", "42G1")
        };

        return new BayView
        {
            Index = bay,
            Number = (bay + 1).ToString("D2"),
            Unloaded = unloaded,
            Remaining = remaining,
            Cells = cells,
            Composition = composition,
            ContainerClass = featured,
            ContainerId = $"{prefix} {(int)(Geo.H(seed) * 900000 + 100000)} {(int)(Geo.H(seed + 3) * 10)}",
            ContainerType = $"{typeName} · {isoCode}",
            Weight = featured == CargoClass.Empty
                ? "3,940 kg"
                : $"{14000 + (int)(Geo.H(seed + 7) * 14000):N0} kg",
            Status = remaining > 0 ? "Aboard" : "Ashore",
            NextMove = remaining > 0 ? typeName : "Bay complete",
            NextEta = remaining > 0 ? $"{21 + bay / 4:D2}:{bay * 13 % 60:D2}" : "—"
        };
    }

    /// <summary>The hover/selection summary shown beside the profile heading.</summary>
    public static string Summary(Vessel vessel, int bay)
    {
        var view = For(vessel, bay);
        var parts = view.Composition
            .Select(entry => $"{entry.Count} {entry.Class.Label().ToLowerInvariant()}")
            .ToList();

        var body = parts.Count > 0 ? string.Join(", ", parts) : "empty";
        return view.Unloaded > 0
            ? $"Bay {view.Number} · {body} · {view.Unloaded} ashore"
            : $"Bay {view.Number} · {body}";
    }
}
