namespace Cargo.Presentation;

/// <summary>Model for the Cargo route. It carries the shared store until the page's own state moves in.</summary>
public sealed class CargoViewModel(PortState state)
{
    public PortState State { get; } = state;
}
