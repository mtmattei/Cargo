using Microsoft.UI.Dispatching;

namespace Cargo.Presentation;

public sealed partial class MainPage : Page
{
    private bool _attached;

    public MainPage()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Attach();
        // xaml-lint: allow responsive - see Attach
        SizeChanged += (_, e) => ViewModel?.ApplyWidth(e.NewSize.Width);
        Loaded += (_, _) => ScrollToStartOffset();
    }

    public MainViewModel? ViewModel => DataContext as MainViewModel;

    // xaml-lint: allow codebehind - the harbour scene and the scanner are code-built controls that take the store directly
    private void Attach()
    {
        if (_attached || ViewModel is not { } vm)
        {
            return;
        }

        _attached = true;
        Harbour.State = vm.State;
        ScannerHost.Content = new ScannerOverlay(vm.State);
        // xaml-lint: allow responsive - a nav label shows by width AND current section; moves to utu:Responsive in the Toolkit pass
        vm.ApplyWidth(ActualWidth > 0 ? ActualWidth : 1680);
        Bindings.Update();
    }

    /// <summary>
    /// Lets a verification run photograph a panel that sits below the fold, which a
    /// background process cannot scroll to with synthesized input.
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG")]
    private void ScrollToStartOffset()
    {
        if (!double.TryParse(Environment.GetEnvironmentVariable("CARGO_SCROLL"), out var offset) || offset <= 0)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low,
            () => SectionScroll.ChangeView(null, offset, null, true));
    }
}
