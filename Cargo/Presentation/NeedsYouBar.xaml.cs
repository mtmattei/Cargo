namespace Cargo.Presentation;

/// <summary>The needs-you bar; Berths shows it in the masthead, Overview in its context panel.</summary>
public sealed partial class NeedsYouBar : UserControl
{
    public NeedsYouBar()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public NeedsYouViewModel? ViewModel => DataContext as NeedsYouViewModel;
}
