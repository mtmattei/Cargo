namespace Cargo.Presentation;

/// <summary>Berths: the berth plan above the vessel you are working.</summary>
public sealed partial class BerthsView : Page
{
    public BerthsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public BerthsViewModel? ViewModel => DataContext as BerthsViewModel;
}
