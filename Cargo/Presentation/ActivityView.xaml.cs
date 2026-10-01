namespace Cargo.Presentation;

public sealed partial class ActivityView : UserControl
{
    public ActivityView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public ActivityViewModel? ViewModel => DataContext as ActivityViewModel;
}
