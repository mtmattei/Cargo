namespace Cargo.Presentation;

public sealed partial class SecurityView : Page
{
    public SecurityView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public SecurityViewModel? ViewModel => DataContext as SecurityViewModel;
}
