namespace Cargo.Presentation;

/// <summary>Model for the Berths route. It carries the shared store until the page's own state moves in.</summary>
public sealed class BerthsViewModel(PortState state)
{
    public PortState State { get; } = state;
}
