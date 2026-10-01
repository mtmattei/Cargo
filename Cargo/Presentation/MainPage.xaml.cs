using Microsoft.UI.Dispatching;

namespace Cargo.Presentation;

public sealed partial class MainPage : Page
{
    private bool _attached;
    private bool _warmed;

    // Scrolling back to the top for Show harbour: no condensing on the way up
    private bool _returning;

    /// <summary>Folding the masthead and the band to its mini frees this much height (121 + 340 - 180).</summary>
    private const double CondenseFrees = 311;

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
        SectionScroll.ViewChanged += OnSectionScrolled;
        // The harbour reaches up behind the masthead by its natural height (not while it folds)
        MastheadFrame.SizeChanged += (_, _) =>
        {
            if (double.IsNaN(MastheadFrame.Height) && MastheadFrame.ActualHeight > 0)
            {
                Harbour.OverlayHeight = MastheadFrame.ActualHeight;
            }
        };
        // Each section opens at its top: one ScrollViewer hosts them all, and a carried offset opened
        // Berths halfway down and brought Overview back scrolled under its full chrome
        vm.State.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PortState.Section))
            {
                _returning = false;
                SectionScroll.ChangeView(null, 0, null, true);
            }
        };
        vm.State.ScrollToTopRequested += (_, _) =>
        {
            _returning = true;
            SectionScroll.ChangeView(null, 0, null, Motion.Reduced);
        };
        Bindings.Update();
    }

    /// <summary>
    /// The fold gives the content 311 px, so the offset a small scroll reached would carry the first section up
    /// under the mini band. A small scroll (within the fold) settles with the section's top 24 px below the band,
    /// clear of its fade; a long jump (PageDown, End, a fling) keeps its offset.
    /// </summary>
    private void SnapToFirstSection(double offset)
    {
        if (overview is not { } view || SectionScroll.Content is not UIElement content)
        {
            return;
        }

        var target = Math.Max(8, view.FirstSectionTop(content) - 24);
        if (offset < target + CondenseFrees + 48)
        {
            SectionScroll.ChangeView(null, target, null, true);
        }
    }

    /// <summary>
    /// Overview condenses once scrolled past 48 px and restores at the top (4 px), so the edge never flaps.
    /// It condenses only when the content stays scrollable after the chrome gives back its 311 px; otherwise
    /// the larger viewport would clamp the offset to the top and restore it straight away.
    /// </summary>
    // xaml-lint: allow codebehind - the scroll offset drives page-chrome state; ViewChanged has no Command surface
    private void OnSectionScrolled(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (ViewModel?.State is not { Section: "overview" } state)
        {
            return;
        }

        var y = SectionScroll.VerticalOffset;
        if (y <= 4)
        {
            _returning = false;
            state.OverviewCondensed = false;
        }
        else if (y > 48 && !_returning && !state.OverviewCondensed && SectionScroll.ScrollableHeight > CondenseFrees + 48)
        {
            state.OverviewCondensed = true;
            SnapToFirstSection(y);
        }
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
