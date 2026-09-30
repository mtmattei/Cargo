namespace Cargo.Presentation;

public sealed partial class OverviewMasthead : UserControl
{
    public OverviewMasthead()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public OverviewViewModel? ViewModel => DataContext as OverviewViewModel;
}
