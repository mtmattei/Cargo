namespace Cargo.Presentation;

/// <summary>Model for the Security route. It carries the shared store until the page's own state moves in.</summary>
public sealed class SecurityViewModel(PortState state)
{
    public PortState State { get; } = state;
}
