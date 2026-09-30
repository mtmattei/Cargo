namespace Cargo.Presentation;

/// <summary>Model for the Overview route. It carries the shared store until the page's own state moves in.</summary>
public sealed class OverviewViewModel(PortState state)
{
    public PortState State { get; } = state;
}
