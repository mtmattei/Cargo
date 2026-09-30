using Microsoft.UI.Dispatching;

namespace Cargo.Presentation;

public sealed partial class MainPage : Page
{
    private bool _attached;

    public MainPage()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Attach();
        Loaded += (_, _) => ScrollToStartOffset();
    }

    public MainViewModel? ViewModel => DataContext as MainViewModel;

    // xaml-lint: allow codebehind - the harbour scene is a code-built control that takes the store directly
    private void Attach()
    {
        if (_attached || ViewModel is not { } vm)
        {
            return;
        }

        _attached = true;
        Harbour.State = vm.State;
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
