using Windows.Foundation;

namespace Cargo.Domain;

/// <summary>
/// The Westhaven data set, transcribed from the "Quay Port v4" design. Everything is
/// synthetic but internally consistent: berths, drafts, schedules and cargo mixes all
/// agree, so the conflict checker and the optimiser have real work to do.
/// </summary>
public static class PortData
{
    /// <summary>Design clock: Wednesday 2 September, 20:58.</summary>
    public const double NowHours = 20 + 58 / 60d;

    public const string Today = "Wednesday 2 September";

    // ── Navigation ────────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<NavSection> Sections = new[]
    {
        // Ordered by how often a duty dispatcher reaches for them over a shift.
        new NavSection("overview", "Overview", "M3 20h18M6 20V10l6-5 6 5v10M10 20v-5h4v5"),
        new NavSection("berths", "Berths", "M3 15l2 5h14l2-5zM5 15V9h14v6M9 9V5h6v4"),
        new NavSection("cargo", "Cargo", "M3 8h18v10H3zM7 8v10M11 8v10M15 8v10"),
        new NavSection("fleet", "Waterways", "M2 12c2-2 4-2 6 0s4 2 6 0 4-2 6 0M4 8l3-4h10l3 4zM12 4v4"),
        new NavSection("security", "Security", "M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6zM9 12l2 2 4-4")
    };

    public static readonly IReadOnlyList<string> JourneyIcons = new[]
    {
        "M3 15l2 5h14l2-5zM5 15V9h14v6",
        "M4 20h16M8 20V6h8M8 6l8 14M12 3v3",
        "M3 14h6v6H3zM9 14h6v6H9zM15 14h6v6h-6z",
        "M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z",
        "M3 7h11v9H3zM14 10h4l3 3v3h-7M6 19a2 2 0 100-4 2 2 0 000 4M17 19a2 2 0 100-4 2 2 0 000 4"
    };

    // ── Vessels in port ───────────────────────────────────────────────────────

    public static readonly IReadOnlyList<Vessel> Vessels = new[]
    {
        new Vessel("aurora", "MSC Aurora", "MSC", "9839272", "Docked", "Docked · Berth 04 · discharging",
            "NavyColor", "04", "Today 14:10", "Thu 06:10", 366, 14.8, 1248, "Cleared",
            "Antwerp → Westhaven → New York", 62, 18, new CargoMix(58, 14, 6, 4, 18), 14,
            "4 cranes working · est. completion 03:40. Loading begins once bays 01–06 are clear.",
            new[]
            {
                new ClearanceCheck("✓", "ISPS declaration", "12:02", "NavyColor"),
                new ClearanceCheck("✓", "Crew list verified", "12:15", "NavyColor"),
                new ClearanceCheck("✓", "Manifest reconciled", "13:48", "NavyColor"),
                new ClearanceCheck("✓", "Dangerous goods plan", "13:50", "NavyColor")
            }),

        new Vessel("nordic", "Nordic Star", "Hapag-Lloyd", "9776171", "Arriving", "Arriving in 42 min · assigned Berth 07",
            "AccentColor", "07", "Today 21:40", "Thu 18:00", 299, 12.9, 816, "Pending",
            "Gothenburg → Westhaven → Halifax", 0, 0, new CargoMix(64, 20, 2, 2, 12), 11,
            "Pilot boarded 20:35 · 2 tugs assigned. Discharge starts ~22:30 after mooring.",
            new[]
            {
                new ClearanceCheck("✓", "ISPS declaration", "18:20", "NavyColor"),
                new ClearanceCheck("✓", "Crew list verified", "18:31", "NavyColor"),
                new ClearanceCheck("○", "Manifest reconciled", "on arrival", "TextFaintColor"),
                new ClearanceCheck("○", "Dangerous goods plan", "on arrival", "TextFaintColor")
            }),

        new Vessel("kaida", "Kaida Maru", "ONE", "9741401", "Docked", "Docked · Berth 01 · loading",
            "NavyColor", "01", "Tue 23:05", "Today 23:30", 334, 13.6, 1092, "Cleared",
            "Westhaven → Singapore → Busan", 100, 74, new CargoMix(70, 8, 3, 5, 14), 13,
            "Discharge complete 09:12. Loading 74% · 3 cranes · sailing 23:30 with the tide.",
            new[]
            {
                new ClearanceCheck("✓", "ISPS declaration", "Tue 20:40", "NavyColor"),
                new ClearanceCheck("✓", "Crew list verified", "Tue 20:52", "NavyColor"),
                new ClearanceCheck("✓", "Manifest reconciled", "Tue 22:30", "NavyColor"),
                new ClearanceCheck("✓", "Dangerous goods plan", "Tue 22:33", "NavyColor")
            }),

        new Vessel("baltic", "Baltic Crown", "Maersk", "9632179", "Departing", "Departing 21:30 · Berth 06",
            "NavyColor", "06", "Tue 16:20", "Today 21:30", 294, 12.1, 640, "Cleared",
            "Westhaven → Rotterdam", 100, 100, new CargoMix(52, 10, 4, 2, 32), 10,
            "Cargo complete · lashing certified 20:15. Pilot due 21:10, tugs standing by.",
            new[]
            {
                new ClearanceCheck("✓", "ISPS declaration", "Tue 14:00", "NavyColor"),
                new ClearanceCheck("✓", "Crew list verified", "Tue 14:12", "NavyColor"),
                new ClearanceCheck("✓", "Manifest reconciled", "Tue 17:05", "NavyColor"),
                new ClearanceCheck("✓", "Departure clearance", "20:40", "NavyColor")
            }),

        new Vessel("levant", "Levant Express", "CMA CGM", "9705523", "At anchor", "At anchor · Outer roads",
            "TextFaintColor", null, "Thu 02:30", "Fri 10:00", 347, 14.2, 1310, "Pending",
            "Piraeus → Westhaven → Montreal", 0, 0, new CargoMix(60, 12, 8, 6, 14), 13,
            "Waiting for Berth 06 to clear after Baltic Crown departs. Assign a berth in Docking.",
            new[]
            {
                new ClearanceCheck("✓", "ISPS declaration", "16:05", "NavyColor"),
                new ClearanceCheck("○", "Crew list verified", "pending", "TextFaintColor"),
                new ClearanceCheck("○", "Manifest reconciled", "pending", "TextFaintColor"),
                new ClearanceCheck("○", "Dangerous goods plan", "pending", "TextFaintColor")
            })
    };

    public static Vessel Vessel(string id) => Vessels.First(v => v.Id == id);

    // ── Berths ────────────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<BerthDef> Berths = new[]
    {
        new BerthDef("01", "occupied", "kaida", 15.2),
        new BerthDef("02", "available", null, 13.0),
        new BerthDef("03", "restricted", null, 12.0),
        new BerthDef("04", "occupied", "aurora", 16.0),
        new BerthDef("05", "available", null, 14.6),
        new BerthDef("06", "occupied", "baltic", 15.0),
        new BerthDef("07", "reserved", "nordic", 13.4),
        new BerthDef("08", "available", null, 13.0)
    };

    /// <summary>Occupation window per vessel, in hours from midnight (may run past 24).</summary>
    public static readonly IReadOnlyDictionary<string, (double Start, double End)> Schedule =
        new Dictionary<string, (double, double)>
        {
            ["kaida"] = (-1, 23.5),
            ["aurora"] = (14.2, 30.2),
            ["baltic"] = (-8, 21.5),
            ["nordic"] = (21.7, 42),
            ["levant"] = (26.5, 58)
        };

    /// <summary>Earlier and planned occupancy drawn behind the live blocks on the 36 h timeline.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<TimelineSegment>> BerthHistory =
        new Dictionary<string, IReadOnlyList<TimelineSegment>>
        {
            ["01"] = new[] { new TimelineSegment("Kaida Maru", 0, 23.5, "occupied") },
            ["02"] = new[] { new TimelineSegment("Sirius Bay", 3, 9.5, "prev"), new TimelineSegment("Elbe Trader", 13, 17, "prev") },
            ["03"] = new[] { new TimelineSegment("Fender repair", 6, 24, "restricted") },
            ["04"] = new[] { new TimelineSegment("Ocean Vela", 0, 5, "prev"), new TimelineSegment("MSC Aurora", 14.2, 30, "occupied") },
            ["05"] = new[] { new TimelineSegment("Aran Wind", 8, 15.5, "prev") },
            ["06"] = new[] { new TimelineSegment("Baltic Crown", 0, 21.5, "occupied"), new TimelineSegment("Levant Express", 22.5, 32, "up") },
            ["07"] = new[] { new TimelineSegment("Corsair Lyra", 2, 10, "prev"), new TimelineSegment("Nordic Star", 21.7, 36, "reserved") },
            ["08"] = new[] { new TimelineSegment("Meridian Sky", 4, 12, "prev") }
        };

    // ── Duty operator ─────────────────────────────────────────────────────────

    /// <summary>The 18:00 to 06:00 shift, so the shift progress means something from the 20:58 start.</summary>
    public static readonly DutyOperator Operator =
        new("Elin Lindqvist", "E. Lindqvist", "EL", "Duty operator", "WH 0417", ClockedIn: 18, ShiftEnd: 30);

    /// <summary>What each berth is showing on the harbour map, independent of the planner.</summary>
    // ── Containers ────────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<ContainerDef> Containers = new[]
    {
        new ContainerDef("MSCU4821937", "40 FT", "Maersk", "Montreal → Rotterdam", "Ready for loading", 2,
            "NavyColor", "22,480 kg", "12.19 × 2.44 × 2.59 m", "Kaida Maru", "Montreal", "Rotterdam",
            "Block B · 14 · Tier 3", "Cleared", "X-ray complete", "MAE-90173-C", "Cleared", "NavyColor"),

        new ContainerDef("CMAU9182044", "40 FT HC", "CMA CGM", "Westhaven → Le Havre", "Manual inspection required", 3,
            "ContainerRustColor", "25,920 kg", "12.19 × 2.44 × 2.90 m", "Nordic Star", "Westhaven", "Le Havre",
            "Inspection lane 2", "On hold", "Weight variance", "CGM-77201-A", "Hold", "OrangeColor", true),

        new ContainerDef("HLCU2209481", "20 FT", "Hapag-Lloyd", "Hamburg → Westhaven", "Discharging · crane 3", 1,
            "OrangeColor", "18,300 kg", "6.06 × 2.44 × 2.59 m", "MSC Aurora", "Hamburg", "Westhaven",
            "—", "Pre-cleared", "Scheduled", "HLC-31820-B", "Cleared", "NavyColor"),

        new ContainerDef("MAEU7710345", "40 FT Reefer", "Maersk", "Valencia → Westhaven", "In yard · reefer block", 2,
            "CargoReeferColor", "24,110 kg", "12.19 × 2.44 × 2.59 m", "MSC Aurora", "Valencia", "Westhaven",
            "Block C · 03 · Tier 1", "Cleared", "Not required", "MAE-44019-R", "Cleared", "NavyColor"),

        new ContainerDef("ONEU3051182", "40 FT", "ONE", "Westhaven → Singapore", "Aboard · Bay 09", 0,
            "ContainerMagentaColor", "21,900 kg", "12.19 × 2.44 × 2.59 m", "Kaida Maru", "Westhaven", "Singapore",
            "—", "Cleared", "X-ray complete", "ONE-11937-D", "Cleared", "NavyColor"),

        new ContainerDef("TGHU6640279", "20 FT", "Textainer", "Westhaven → Chicago", "Loaded on rail", 4,
            "ContainerOliveColor", "17,640 kg", "6.06 × 2.44 × 2.59 m", "MSC Aurora", "Antwerp", "Chicago",
            "Rail head 2", "Cleared", "Complete", "TGH-52210-E", "Cleared", "NavyColor"),

        new ContainerDef("CSQU1905536", "45 FT", "COSCO", "Shanghai → Westhaven", "Awaiting customs", 3,
            "NavyColor", "27,300 kg", "13.72 × 2.44 × 2.90 m", "MSC Aurora", "Shanghai", "Westhaven",
            "Customs area", "Review", "Documentary", "COS-88120-F", "Pending", "AmberDeepColor"),

        new ContainerDef("SEGU4839200", "20 FT Hazmat", "Seaco", "Antwerp → Westhaven", "In yard · hazmat block", 2,
            "CargoHazardColor", "19,880 kg", "6.06 × 2.44 × 2.59 m", "MSC Aurora", "Antwerp", "Westhaven",
            "Block D · 07 · Tier 1", "Cleared", "Complete", "SEA-20941-H", "Cleared", "NavyColor"),

        new ContainerDef("GESU5529013", "40 FT", "GE Seaco", "Westhaven → Toronto", "Gate out · truck", 4,
            "ContainerSageColor", "20,050 kg", "12.19 × 2.44 × 2.59 m", "Baltic Crown", "Rotterdam", "Toronto",
            "—", "Cleared", "Complete", "GES-71100-K", "Cleared", "NavyColor"),

        new ContainerDef("TCLU8371628", "40 FT HC", "Triton", "Rotterdam → Westhaven", "Aboard · Bay 03", 0,
            "TextMutedColor", "23,410 kg", "12.19 × 2.44 × 2.90 m", "MSC Aurora", "Rotterdam", "Westhaven",
            "—", "Pre-cleared", "Scheduled", "TRI-60021-M", "Cleared", "NavyColor")
    };

    public static readonly IReadOnlyList<string> ContainerFilters = new[] { "All", "Aboard", "Yard", "Inspection", "Outbound" };

    public static readonly IReadOnlyDictionary<string, int[]> FilterStages = new Dictionary<string, int[]>
    {
        ["Aboard"] = new[] { 0, 1 },
        ["Yard"] = new[] { 2 },
        ["Inspection"] = new[] { 3 },
        ["Outbound"] = new[] { 4 }
    };

    public static readonly IReadOnlyList<string> JourneyStages = new[] { "Ship", "Crane", "Yard", "Inspection", "Truck / Rail" };

    /// <summary>What the X-ray finds inside each container.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<CargoItem>> Manifests =
        new Dictionary<string, IReadOnlyList<CargoItem>>
        {
            ["MSCU4821937"] = new[]
            {
                new CargoItem("Machined parts · crates", "ScanMetallicColor"),
                new CargoItem("Textiles · bales", "ScanOrganicColor"),
                new CargoItem("Paper rolls", "ScanOrganicColor"),
                new CargoItem("Steel fittings", "ScanMetallicColor"),
                new CargoItem("Packaging", "ScanMixedColor"),
                new CargoItem("Pallets · mixed", "ScanMixedColor")
            },
            ["CMAU9182044"] = new[]
            {
                new CargoItem("Industrial pump · crated", "ScanMetallicColor"),
                new CargoItem("Spare parts", "ScanMetallicColor"),
                new CargoItem("Dense mass · unlisted", "ScanAnomalyColor", true),
                new CargoItem("Foam packaging", "ScanOrganicColor"),
                new CargoItem("Tooling", "ScanMetallicColor")
            },
            ["HLCU2209481"] = new[]
            {
                new CargoItem("Furniture · flatpack", "ScanOrganicColor"),
                new CargoItem("Furniture · flatpack", "ScanOrganicColor"),
                new CargoItem("Hardware", "ScanMetallicColor"),
                new CargoItem("Foam", "ScanOrganicColor")
            },
            ["MAEU7710345"] = new[]
            {
                new CargoItem("Frozen fish · −18°C", "ScanMixedColor"),
                new CargoItem("Frozen fish · −18°C", "ScanMixedColor"),
                new CargoItem("Frozen fish · −18°C", "ScanMixedColor"),
                new CargoItem("Reefer unit", "ScanMetallicColor")
            },
            ["SEGU4839200"] = new[]
            {
                new CargoItem("Drums · IMDG 3", "ScanAnomalyColor"),
                new CargoItem("Drums · IMDG 3", "ScanAnomalyColor"),
                new CargoItem("Absorbent", "ScanOrganicColor")
            }
        };

    public static readonly IReadOnlyList<CargoItem> DefaultManifest = new[]
    {
        new CargoItem("Consumer goods", "ScanOrganicColor"),
        new CargoItem("Electronics", "ScanMetallicColor"),
        new CargoItem("Mixed pallets", "ScanMixedColor"),
        new CargoItem("Consumer goods", "ScanOrganicColor"),
        new CargoItem("Packaging", "ScanMixedColor")
    };

    // ── Fleet & waterways ─────────────────────────────────────────────────────

    public const double RiverKm = 120;

    /// <summary>The Alder river, Westhaven (km 0, right) to Alder Basin (km 120, left).</summary>
    public static readonly IReadOnlyList<Point> River = new[]
    {
        new Point(1400, 300), new Point(1240, 290), new Point(1090, 330), new Point(940, 300),
        new Point(800, 240), new Point(660, 250), new Point(520, 300), new Point(380, 332),
        new Point(240, 290), new Point(100, 250)
    };

    public static readonly IReadOnlyList<RiverVessel> RiverFleet = new[]
    {
        new RiverVessel("ws204", "Westhaven Spirit", "WO-7741", 22, 9.4, 1, 22.3, 0, "Cpt. R. Vandermeer", "Lock 1 · Halden", false,
            new[] { new ChatLine("crew", "Passed km 20, holding 9.4 kn. Halden lock booked 21:25.", 20.3) }),

        new RiverVessel("al118", "Alder Trader", "WO-7736", 58, 6.1, 1, 23.9, 25, "Cpt. M. Osei", "Lock 2 · Brenner", true,
            new[]
            {
                new ChatLine("crew", "Brenner lock reports 40 min queue. Expect slip.", 20.1),
                new ChatLine("dispatch", "Acknowledged. Consignee informed, new window 23:50.", 20.2)
            }),

        new RiverVessel("nb330", "Nordbank", "WO-7729", 91, 5.2, -1, 25.6, 0, "Cpt. L. Fjeld", "Lock 3 · Ravensmoor", false,
            new[] { new ChatLine("crew", "Downbound from Alder Basin, draft 2.6 m.", 19.6) }),

        new RiverVessel("tg07", "Tug Kestrel", "ASSIST-Nordic Star", 4, 7.8, -1, 21.4, 0, "Skipper J. Brandt", "Sound · pilot station", false,
            new[] { new ChatLine("crew", "Proceeding to meet Nordic Star at the fairway buoy.", 20.6) }),

        new RiverVessel("el552", "Elbe Courier", "WO-7744", 109, 4.6, -1, 27.9, 55, "Cpt. A. Novak", "Low-water stretch km 85", true,
            new[]
            {
                new ChatLine("crew", "Reduced to 4.6 kn — gauge Ravensmoor 2.9 m, keeping mid-channel.", 20.4),
                new ChatLine("dispatch", "Copy. Hold speed; do not attempt night passage of km 85 below 2.8 m.", 20.5)
            }),

        new RiverVessel("ws219", "Westhaven Pride", "WO-7738", 41, 8.7, -1, 23.1, 0, "Cpt. S. Ibarra", "Lock 1 · Halden", false,
            new[] { new ChatLine("crew", "Cleared Brenner 19:50. Downbound.", 19.9) })
    };

    public static readonly IReadOnlyList<GaugeDef> Gauges = new[]
    {
        new GaugeDef("Sound bar", 12, 4.8, "SeaGreenColor", "↑"),
        new GaugeDef("Halden", 44, 3.6, "SeaGreenColor", "→"),
        new GaugeDef("Ravensmoor", 85, 2.9, "AlertColor", "↓"),
        new GaugeDef("Alder Basin", 112, 3.1, "OrangeColor", "↓")
    };

    public static readonly IReadOnlyList<LockDef> Locks = new[]
    {
        new LockDef("Lock 1 · Halden", 28, "Open · upbound", "SeaGreenColor"),
        new LockDef("Lock 2 · Brenner", 64, "Queue 40 min", "OrangeColor"),
        new LockDef("Lock 3 · Ravensmoor", 97, "Maintenance 22:00–23:30", "AlertColor")
    };

    public static readonly IReadOnlyList<NoticeDef> Notices = new[]
    {
        new NoticeDef("Low water · km 85", "Ravensmoor gauge 2.9 m, falling. Max draft 2.6 m after 23:00.", 85, "AlertColor"),
        new NoticeDef("Lock 3 maintenance", "Ravensmoor closed 22:00–23:30 for gate hydraulics.", 97, "AlertColor"),
        new NoticeDef("Strong cross-current · km 40", "Ebb sets NE across the Halden bend, 1.8 kn until 23:10.", 40, "OrangeColor")
    };

    public static readonly IReadOnlyList<LockingDef> Lockings = new[]
    {
        new LockingDef("20:40", "Lock 1", "Westhaven Pride", "Done"),
        new LockingDef("21:25", "Lock 1", "Westhaven Spirit", "Booked"),
        new LockingDef("21:50", "Lock 2", "Alder Trader", "Queued"),
        new LockingDef("22:10", "Lock 2", "Westhaven Pride", "Booked"),
        new LockingDef("23:35", "Lock 3", "Nordbank", "After maintenance"),
        new LockingDef("00:20", "Lock 3", "Elbe Courier", "At risk")
    };

    // ── Yard ──────────────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<YardZoneDef> YardZones = new[]
    {
        new YardZoneDef("A", "Block A", "Standard", 40, 30, 470, 220, "CargoStandardColor", false),
        new YardZoneDef("B", "Block B", "Standard", 590, 30, 470, 220, "CargoStandardColor", false),
        new YardZoneDef("C", "Block C", "Refrigerated", 40, 310, 300, 220, "CargoReeferColor", false),
        new YardZoneDef("D", "Block D", "Hazardous", 360, 310, 150, 220, "CargoHazardColor", true),
        new YardZoneDef("E", "Inspection", "Awaiting exam", 590, 310, 220, 220, "CargoOversizeColor", true),
        new YardZoneDef("F", "Restricted", "Bonded", 830, 310, 230, 220, "RestrictedColor", true)
    };

    public static readonly IReadOnlyList<string> ContainerPrefixes = new[]
    {
        "MSCU", "MAEU", "HLCU", "CMAU", "ONEU", "TCLU", "SEGU", "CSQU"
    };

    // ── Security ──────────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<SecurityZoneDef> SecurityZones = new[]
    {
        new SecurityZoneDef("Vessel access", "Quay · escorted", 100, 104, 420, 80, "AccentColor", 0.10, false),
        new SecurityZoneDef("Vessel access", "Quay · escorted", 580, 104, 420, 80, "AccentColor", 0.10, false),
        new SecurityZoneDef("Container inspection", "X-ray · lanes 1–3", 100, 210, 200, 170, "AmberDeepColor", 0.14, false),
        new SecurityZoneDef("Customs area", "Bonded holding", 320, 210, 200, 170, "AccentColor", 0.12, false),
        new SecurityZoneDef("Restricted zone", "Fuel & hazmat", 580, 210, 180, 170, "RestrictedColor", 0.14, true),
        new SecurityZoneDef("Yard · general", "", 780, 210, 220, 170, "ContainerSageColor", 0.10, false),
        new SecurityZoneDef("Main entrance", "Personnel & visitors", 60, 430, 220, 34, "AccentColor", 0.14, false),
        new SecurityZoneDef("Truck gates", "Gates 1–3", 540, 430, 480, 34, "AmberDeepColor", 0.10, false)
    };

    public static readonly IReadOnlyList<Point> Cameras = new[]
    {
        new Point(120, 180), new Point(500, 180), new Point(1000, 180),
        new Point(300, 400), new Point(560, 400), new Point(1000, 400)
    };

    public static readonly IReadOnlyList<(string Label, double X, double Y)> Gates = new[]
    {
        ("G1", 555d, 391d), ("G2", 695d, 391d), ("G3", 835d, 391d), ("Main", 105d, 391d)
    };

    public static readonly IReadOnlyList<Point> AccessPoints = new[]
    {
        new Point(95, 250), new Point(315, 250), new Point(575, 250),
        new Point(775, 250), new Point(95, 140), new Point(575, 140)
    };

    public static readonly IReadOnlyList<SecurityEventDef> SecurityEvents = new[]
    {
        new SecurityEventDef(6.5, "Gate open", "AccentColor"),
        new SecurityEventDef(7.2, "Kaida Maru cleared", "AccentColor"),
        new SecurityEventDef(8.1, "X-ray lane 2", "AccentColor"),
        new SecurityEventDef(9.4, "Driver ID rejected", "RestrictedColor"),
        new SecurityEventDef(11, "Customs batch 14", "AccentColor"),
        new SecurityEventDef(12.5, "Aurora ISPS", "AccentColor"),
        new SecurityEventDef(14.2, "Aurora alongside", "AccentColor"),
        new SecurityEventDef(16.7, "CSQU hold", "OrangeColor"),
        new SecurityEventDef(18.3, "Seal audit", "AccentColor"),
        new SecurityEventDef(19.6, "CMAU weight hold", "OrangeColor"),
        new SecurityEventDef(20.7, "Baltic dep. clearance", "AccentColor")
    };

    public static readonly IReadOnlyList<SecurityRecordDef> SecurityRecords = new[]
    {
        new SecurityRecordDef("Container seal validation", "412", "AccentColor"),
        new SecurityRecordDef("Customs clearance", "388", "AccentColor"),
        new SecurityRecordDef("Cargo inspection", "38", "AccentColor"),
        new SecurityRecordDef("Driver verification", "221", "AccentColor"),
        new SecurityRecordDef("Vehicle access", "236", "AccentColor"),
        new SecurityRecordDef("Employee access", "612", "AccentColor"),
        new SecurityRecordDef("Restricted zone access", "19", "RestrictedColor"),
        new SecurityRecordDef("Security incidents", "1", "OrangeColor")
    };

    public static readonly IReadOnlyList<AccessZoneDef> AccessZones = new[]
    {
        new AccessZoneDef("Quay / vessel access", 0, 0, 520, 40, "AccentColor"),
        new AccessZoneDef("Inspection", 20, 56, 120, 60, "AmberDeepColor"),
        new AccessZoneDef("Customs", 150, 56, 120, 60, "AccentColor"),
        new AccessZoneDef("Restricted", 280, 56, 100, 60, "RestrictedColor"),
        new AccessZoneDef("Yard", 390, 56, 110, 60, "ContainerSageColor"),
        new AccessZoneDef("Admin", 20, 130, 150, 50, "TextMutedColor"),
        new AccessZoneDef("Gates", 190, 130, 190, 50, "AmberDeepColor"),
        new AccessZoneDef("Rail head", 390, 130, 110, 50, "ContainerOliveColor")
    };

    public static readonly IReadOnlyList<PersonDef> People = new[]
    {
        new PersonDef("Elena Marsh", "Berth supervisor", "WHV-0142 · Level 3", new[] { 0, 1, 2, 4, 5, 6, 7 }, "AccentColor"),
        new PersonDef("Tomas Okafor", "Crane operator", "WHV-2210 · Level 2", new[] { 0, 4 }, "AccentColor"),
        new PersonDef("Priya Nair", "Customs officer", "CBSA-7731 · Federal", new[] { 1, 2, 3, 5, 6 }, "AmberDeepColor"),
        new PersonDef("Marcus Feld", "Security patrol", "WHV-0907 · Level 4", new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, "RestrictedColor")
    };

    public static readonly IReadOnlyList<VehicleDef> Vehicles = new[]
    {
        new VehicleDef("TRK 4471", "J. Alvarez", "GESU 552901 3", "Gate 2 · out", "Authorized", "AccentColor", "19:48", "ContainerSageColor"),
        new VehicleDef("TRK 1029", "S. Brennan", "MSCU 482193 7", "Gate 1 · in", "Authorized", "AccentColor", "20:12", "AccentColor"),
        new VehicleDef("TRK 8834", "—", "—", "Gate 3 · in", "Awaiting driver ID", "OrangeColor", "20:41", "CargoEmptyColor"),
        new VehicleDef("TRK 2207", "L. Kowalski", "MAEU 771034 5", "Gate 2 · in", "Authorized", "AccentColor", "20:52", "CargoReeferColor")
    };

    public static readonly IReadOnlyList<InspectionStepDef> InspectionSteps = new[]
    {
        new InspectionStepDef("Manifest verification", "Matched · 47 line items", "ok", 150, 66),
        new InspectionStepDef("Seal inspection", "CGM-77201-A intact", "ok", 450, 54),
        new InspectionStepDef("Weight verification", "+1.8 t over declared", "warn", 750, 66),
        new InspectionStepDef("X-ray scan", "Complete · dense mass, bay 3", "ok", 150, 454),
        new InspectionStepDef("Physical inspection", "Scheduled 21:15 · Lane 2", "pending", 450, 466),
        new InspectionStepDef("Customs approval", "Awaiting inspection", "pending", 750, 454)
    };

    public static readonly IReadOnlyList<InspectionLogDef> InspectionLog = new[]
    {
        new InspectionLogDef("18:02", "✓", "Gate 1 entry · TRK 3391 · driver verified", "AccentColor"),
        new InspectionLogDef("18:09", "✓", "Seal CGM-77201-A photographed and matched", "AccentColor"),
        new InspectionLogDef("18:11", "✓", "Manifest reconciled with booking 88210", "AccentColor"),
        new InspectionLogDef("19:36", "!", "Weighbridge 2: 25,920 kg vs declared 24,120 kg", "OrangeColor"),
        new InspectionLogDef("19:40", "✓", "X-ray lane 2: dense mass detected bay 3, consistent with machinery", "AccentColor"),
        new InspectionLogDef("19:42", "○", "Hold placed · physical inspection scheduled 21:15", "TextFaintColor")
    };

    // ── Activity ──────────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<ActivityDef> Activity = new[]
    {
        new ActivityDef("20:52", "gate", "TRK 2207 entered Gate 2", "L. Kowalski · MAEU 771034 5 · reefer pickup"),
        new ActivityDef("20:41", "warn", "Driver ID pending at Gate 3", "TRK 8834 · awaiting credential match"),
        new ActivityDef("20:35", "vessel", "Pilot boarded Nordic Star", "ETA Berth 07 21:40 · tugs Orca and Kestrel assigned"),
        new ActivityDef("20:15", "security", "Baltic Crown lashing certified", "Departure clearance issued 20:40"),
        new ActivityDef("19:42", "warn", "Hold placed on CMAU 918204 4", "Weight variance +1.8 t · physical inspection 21:15"),
        new ActivityDef("18:30", "container", "MSC Aurora discharge passed 50%", "624 of 1,248 · 4 cranes · 41 moves/hr avg"),
        new ActivityDef("16:45", "security", "CSQU 190553 6 held for documentary review", "Customs · COSCO · Shanghai origin"),
        new ActivityDef("14:10", "vessel", "MSC Aurora alongside Berth 04", "First lift 14:52"),
        new ActivityDef("12:00", "vessel", "Meridian Sky departed Berth 08", "Bound Liverpool"),
        new ActivityDef("09:12", "container", "Kaida Maru discharge complete", "1,092 TEU · loading started 09:40"),
        new ActivityDef("07:20", "security", "Kaida Maru ISPS clearance renewed", "Level 1 · no findings"),
        new ActivityDef("06:30", "gate", "Truck gates opened", "Gates 1–3 · 236 vehicles processed since")
    };

    public static readonly IReadOnlyDictionary<string, (string Icon, string Tone)> ActivityIcons =
        new Dictionary<string, (string, string)>
        {
            ["gate"] = ("M3 7h11v9H3zM14 10h4l3 3v3h-7M6 19a2 2 0 100-4 2 2 0 000 4M17 19a2 2 0 100-4 2 2 0 000 4", "AccentColor"),
            ["warn"] = ("M12 3l10 18H2zM12 10v5M12 18v.5", "OrangeColor"),
            ["vessel"] = ("M3 15l2 5h14l2-5zM5 15V9h14v6M9 9V5h6v4", "InkRaisedColor"),
            ["security"] = ("M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6zM9 12l2 2 4-4", "AccentColor"),
            ["container"] = ("M3 8h18v10H3zM7 8v10M11 8v10M15 8v10", "AccentColor")
        };

    // ── Series ────────────────────────────────────────────────────────────────

    /// <summary>Arrivals per hour, midnight to midnight.</summary>
    public static readonly IReadOnlyList<double> Arrivals = new[]
    {
        0.2, 0.2, 0.4, 0.9, 1, 0.9, 0.6, 0.5, 0.5, 0.9, 1.3, 1.4, 1.4,
        1.1, 0.7, 0.5, 0.6, 0.9, 1.2, 1.3, 1.2, 1.0, 0.5, 0.4, 0.3
    };

    public static readonly IReadOnlyList<double> Departures = new[]
    {
        0.1, 0.1, 0.2, 0.3, 0.6, 0.8, 0.7, 0.5, 0.4, 0.5, 0.7, 0.9, 1,
        0.9, 0.7, 0.6, 0.7, 0.8, 0.7, 0.5, 0.6, 0.9, 0.5, 0.3, 0.2
    };

    public static readonly IReadOnlyList<(string Name, double Hour, bool Arrival)> Movements = new[]
    {
        ("Sirius Bay", 3d, true),
        ("Corsair Lyra", 2.4d, true),
        ("Ocean Vela dep.", 4.8d, false),
        ("Meridian Sky dep.", 12d, false),
        ("MSC Aurora", 14.2d, true),
        ("Elbe Trader dep.", 17d, false),
        ("Nordic Star", 21.7d, true),
        ("Baltic Crown dep.", 21.5d, false)
    };

    /// <summary>Container moves per hour.</summary>
    public static readonly IReadOnlyList<double> Volume = new[]
    {
        40d, 32, 28, 26, 30, 44, 80, 120, 150, 170, 182, 176, 160,
        168, 178, 186, 190, 182, 170, 168, 175, 184, 150, 120, 90
    };

    public static readonly IReadOnlyList<double> CargoIn = new[]
    {
        10d, 8, 7, 9, 14, 30, 55, 80, 95, 110, 118, 112, 100,
        104, 110, 116, 120, 114, 108, 102, 96, 98, 80, 60, 40
    };

    public static readonly IReadOnlyList<double> CargoOut = new[]
    {
        30d, 26, 22, 20, 24, 40, 70, 100, 125, 140, 150, 156, 158,
        162, 168, 170, 166, 160, 150, 140, 130, 122, 100, 70, 45
    };

    /// <summary>Tide from 18:00 to 06:00 in half-hour steps. HW 23:10, +2.1 m.</summary>
    public static IReadOnlyList<double> Tide { get; } = Enumerable
        .Range(0, 25)
        .Select(i => 1.1 + 1.0 * Math.Cos((18 + i * 0.5 - 23.17) / 12.4 * 2 * Math.PI))
        .ToArray();

    public static double TideAt(double hour) => 1.1 + 1.0 * Math.Cos((hour - 23.17) / 12.4 * 2 * Math.PI);
}
