using Cargo.Controls;
using Microsoft.UI.Xaml.Media;

namespace Cargo.Presentation;

/// <summary>Berths: the berth plan above the vessel you are working.</summary>
public sealed partial class BerthsView : Page
{
    private bool _attached;

    public BerthsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    /// <summary>The router builds the page, then hands it its model; the live work starts there.</summary>
    private void Attach()
    {
        if (_attached || DataContext is not BerthsViewModel vm)
        {
            return;
        }

        _attached = true;
        State = vm.State;
        var state = vm.State;

        PlanHost.Content = new DockingView(state);
        VesselHost.Content = new VesselsView(state);

        this.RebuildWhenVisible(state, Bindings.Update);
        Bindings.Update();
    }

    public PortState State { get; private set; } = null!;

    public string Alongside => PortData.Berths.Count(b => b.State == "occupied").ToString();

    public string Arriving => PortData.Vessels.Count(v => v.Status == "Arriving").ToString();

    public string AtAnchor => PortData.Vessels.Count(v => v.Status == "At anchor").ToString();

    public string Conflicts => State.Conflicts().Count.ToString();

    public Brush ConflictInk => State.Conflicts().Count > 0 ? Tokens.Brush("AlertBrush") : Tokens.Brush("InkBrush");
}
