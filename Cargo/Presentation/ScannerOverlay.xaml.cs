namespace Cargo.Presentation;

/// <summary>
/// The full-window container inspection. It lives in the shell rather than inside the
/// Cargo page so it covers the whole app the way the design's fixed overlay does.
/// </summary>
public sealed partial class ScannerOverlay : UserControl
{
    public ScannerOverlay()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            // xaml-lint: allow codebehind - the 3D scanner is a code-built panel that reads the store directly
            if (ViewModel is { } vm)
            {
                Scanner3D.State = vm.State;
            }

            Bindings.Update();
        };
    }

    public ScannerViewModel? ViewModel => DataContext as ScannerViewModel;
}
