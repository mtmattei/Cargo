namespace Cargo.Presentation;

public sealed partial class OverviewView : Page
{
    public OverviewView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public OverviewViewModel? ViewModel => DataContext as OverviewViewModel;
}
