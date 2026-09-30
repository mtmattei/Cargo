using System.Windows.Input;
using Cargo.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Cargo.Presentation;

public sealed class RecordRow
{
    public required string Label { get; init; }
    public required string Count { get; init; }
    public required Brush Tone { get; init; }
}

public sealed class PersonCard
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required string Role { get; init; }
    public required string Credential { get; init; }
    public required string Initials { get; init; }
    public required Brush Avatar { get; init; }
    public required Brush Border { get; init; }
    public required ICommand Select { get; init; }
}

public sealed class VehicleCard
{
    public required Color Tint { get; init; }
    public required IReadOnlyList<Fact> Facts { get; init; }
}

public sealed partial class SecurityView : Page
{
    private static readonly (string Id, string Label)[] TabDefinitions =
    {
        ("zones", "Zones"), ("access", "Access control"), ("inspection", "Inspection")
    };

    private readonly AccessZoneMap _accessMap = new();

    private bool _attached;

    public SecurityView()
    {
        Tabs = new ObservableCollection<FilterChip>();
        People = new ObservableCollection<PersonCard>();

        ClearedChecks = new[]
        {
            "Seal verified", "Manifest matched", "Customs cleared", "X-ray inspection complete"
        }.Select(label => new CheckRow
        {
            Mark = "✓",
            Label = label,
            Time = string.Empty,
            Tone = Tokens.Brush("TealBrush")
        }).ToList();

        HeldChecks = new[]
        {
            ("✓", "Seal verified", "TealBrush"),
            ("✓", "Manifest matched", "TealBrush"),
            ("⚠", "Weight variance +1.8 t · manual inspection required", "OrangeBrush")
        }.Select(entry => new CheckRow
        {
            Mark = entry.Item1,
            Label = entry.Item2,
            Time = string.Empty,
            Tone = Tokens.Brush(entry.Item3)
        }).ToList();

        Records = PortData.SecurityRecords
            .Select(r => new RecordRow { Label = r.Label, Count = r.Count, Tone = Tokens.Brush(r.Tone) })
            .ToList();

        InspectionLog = PortData.InspectionLog
            .Select(l => new CheckRow { Mark = l.Mark, Label = l.Text, Time = l.Time, Tone = Tokens.Brush(l.Tone) })
            .ToList();

        Vehicles = PortData.Vehicles.Select(v => new VehicleCard
        {
            Tint = Tokens.Color(v.TintToken),
            Facts = new[]
            {
                new Fact { Key = "Truck", Value = v.Id },
                new Fact { Key = "Driver", Value = v.Driver },
                new Fact { Key = "Container", Value = v.Container },
                new Fact { Key = "Gate", Value = v.Gate },
                new Fact { Key = "Authorization", Value = v.Authorization, Tone = Tokens.Brush(v.AuthToken) },
                new Fact { Key = "Entry", Value = v.Entry }
            }
        }).ToList();

        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    /// <summary>The router builds the page, then hands it its model; the live work starts there.</summary>
    private void Attach()
    {
        if (_attached || DataContext is not SecurityViewModel vm)
        {
            return;
        }

        _attached = true;
        State = vm.State;
        var state = vm.State;

        ZoneMapHost.Content = new SecurityZoneMap();
        TimelineHost.Content = new SecurityTimeline();
        AccessMapHost.Content = _accessMap;
        InspectionHost.Content = new InspectionDiagram();

        Refresh();
        this.RebuildWhenVisible(state, Refresh);
        Bindings.Update();
    }

    public PortState State { get; private set; } = null!;

    public ObservableCollection<FilterChip> Tabs { get; }
    public ObservableCollection<PersonCard> People { get; }
    public IReadOnlyList<CheckRow> ClearedChecks { get; }
    public IReadOnlyList<CheckRow> HeldChecks { get; }
    public IReadOnlyList<RecordRow> Records { get; }
    public IReadOnlyList<CheckRow> InspectionLog { get; }
    public IReadOnlyList<VehicleCard> Vehicles { get; }

    public string Title { get; private set; } = string.Empty;
    public string ZoneHeading { get; private set; } = string.Empty;
    public string ZoneCount { get; private set; } = string.Empty;

    private void Refresh()
    {
        Title = State.SecurityTitle;

        Tabs.Clear();
        foreach (var (id, label) in TabDefinitions)
        {
            var current = State.SecurityTab == id;
            Tabs.Add(new FilterChip
            {
                Label = label,
                Background = current ? Tokens.Brush("SurfaceBrush") : Tokens.Transparent,
                Foreground = current ? Tokens.Brush("InkBrush") : Tokens.Brush("TextMutedBrush"),
                Border = Tokens.Transparent,
                Apply = new RelayCommand<string>(value =>
                    State.SecurityTab = TabDefinitions.First(t => t.Label == value).Id)
            });
        }

        ZonesTab.Visibility = State.SecurityTab == "zones" ? Visibility.Visible : Visibility.Collapsed;
        AccessTab.Visibility = State.SecurityTab == "access" ? Visibility.Visible : Visibility.Collapsed;
        InspectionTab.Visibility = State.SecurityTab == "inspection" ? Visibility.Visible : Visibility.Collapsed;

        BuildPeople();
        Bindings.Update();
    }

    private void BuildPeople()
    {
        People.Clear();
        for (var i = 0; i < PortData.People.Count; i++)
        {
            var person = PortData.People[i];
            People.Add(new PersonCard
            {
                Index = i,
                Name = person.Name,
                Role = person.Role,
                Credential = person.Credential,
                Initials = string.Concat(person.Name.Split(' ').Select(part => part[0])),
                Avatar = Tokens.Brush(person.AvatarToken),
                Border = State.SelectedPerson == i ? Tokens.Brush("InkBrush") : Tokens.Brush("HairlineBrush"),
                Select = new RelayCommand<int>(index => State.SelectedPerson = index)
            });
        }

        var selected = PortData.People[State.SelectedPerson];
        ZoneHeading = $"{selected.Name} · authorized zones";
        ZoneCount = $"{selected.Zones.Count} of {PortData.AccessZones.Count}";
        _accessMap.Show(selected.Zones);
    }
}
