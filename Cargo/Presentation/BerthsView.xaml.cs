using Cargo.Controls;
using Microsoft.UI.Xaml.Media;

namespace Cargo.Presentation;

/// <summary>Berths: the berth plan (was Docking) above the vessel you are working (was Vessels).</summary>
public sealed partial class BerthsView : UserControl
{
    public BerthsView(PortState state)
    {
        State = state;
        InitializeComponent();

        PlanHost.Content = new DockingView(state);
        VesselHost.Content = new VesselsView(state);

        this.RebuildWhenVisible(state, Bindings.Update);
    }

    public PortState State { get; }

    public string Alongside => PortData.Berths.Count(b => b.State == "occupied").ToString();

    public string Arriving => PortData.Vessels.Count(v => v.Status == "Arriving").ToString();

    public string AtAnchor => PortData.Vessels.Count(v => v.Status == "At anchor").ToString();

    public string Conflicts => State.Conflicts().Count.ToString();

    public Brush ConflictInk => State.Conflicts().Count > 0 ? Tokens.Brush("AlertBrush") : Tokens.Brush("InkBrush");
}
