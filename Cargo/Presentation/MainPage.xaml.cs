using Microsoft.UI.Dispatching;

namespace Cargo.Presentation;

public sealed partial class MainPage : Page
{
    private bool _attached;
    private bool _warmed;

    public MainPage()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Attach();

        Loaded += (_, _) =>
        {
            ScrollToStartOffset();
            WarmSections();
        };
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
    /// Builds the sections not yet shown, one per idle turn after the first frame. A section
    /// costs the better part of a second to build, and paying that on the first click is what
    /// made a first visit feel like a stall.
    /// </summary>
    // xaml-lint: allow codebehind - x:Load has no XAML trigger for "after startup, when idle"
    private void WarmSections()
    {
        // Loaded fires again when the page is re-parented; the sections only need building once
        if (_warmed)
        {
            return;
        }

        _warmed = true;
        var pending = new Queue<string>(PortData.Sections.Select(s => s.Id));

        void Next()
        {
            if (pending.TryDequeue(out var id))
            {
                FindName(id);
                DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, Next);
            }
        }

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, Next);
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
