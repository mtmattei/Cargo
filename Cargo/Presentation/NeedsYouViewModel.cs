using Microsoft.UI.Xaml.Media;

namespace Cargo.Presentation;

/// <summary>
/// The needs-you bar: the one decision waiting on the dispatcher, made in place.
/// needs → busy (the confirm is in flight, ~600 ms) → done (with Undo) → needs again on Undo.
/// With nothing pending it reads "Nothing needs you" in the done style; it is never hidden,
/// so the masthead does not jump. Created by <see cref="BerthsViewModel"/> on the UI thread.
/// </summary>
public sealed partial class NeedsYouViewModel : ObservableObject
{
    private static readonly TimeSpan ConfirmLatency = TimeSpan.FromMilliseconds(600);

    private readonly PortState _state;
    private string? _confirmed;
    private string? _confirmedAt;
    private bool _busy;
    private string? _error;

    public NeedsYouViewModel(PortState state)
    {
        _state = state;
        Refresh();
    }

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _meta = string.Empty;
    [ObservableProperty] private string _confirmLabel = string.Empty;
    [ObservableProperty] private Brush? _ground;
    [ObservableProperty] private Brush? _edge;
    [ObservableProperty] private Brush? _metaForeground;
    [ObservableProperty] private Visibility _needsMark;
    [ObservableProperty] private Visibility _doneMark;
    [ObservableProperty] private Visibility _reviewVisibility;
    [ObservableProperty] private Visibility _confirmVisibility;
    [ObservableProperty] private Visibility _undoVisibility;
    [ObservableProperty] private bool _isBusy;

    /// <summary>Hovering the bar outlines its vessel, lights its tag and its row.</summary>
    public System.Windows.Input.ICommand HoverCommand => _state.HoverCommand;

    /// <summary>The vessel the bar is about, for hover linking; null when nothing is pending.</summary>
    public string? Subject => _confirmed ?? _state.PendingDecision;

    [RelayCommand]
    private async Task Confirm()
    {
        if (_busy || _state.PendingDecision is not { } vessel)
        {
            return;
        }

        _busy = true;
        _error = null;
        Refresh();

        await Task.Delay(ConfirmLatency);

        _busy = false;
        _error = _state.Confirm(vessel);
        if (_error is null)
        {
            _confirmed = vessel;
            _confirmedAt = PortState.Format(_state.NowHours);
        }

        Refresh();
    }

    [RelayCommand]
    private void Review()
    {
        if (Subject is { } vessel)
        {
            _state.PickVessel(vessel);
        }
    }

    [RelayCommand]
    private void Undo()
    {
        if (_confirmed is { } vessel)
        {
            _confirmed = null;
            _state.UndoConfirm(vessel);
            Refresh();
        }
    }

    /// <summary>Called by the page while it is on screen, on structure changes and each tick.</summary>
    public void Refresh()
    {
        var pending = _state.PendingDecision;
        var done = pending is null;

        if (_confirmed is { } confirmed)
        {
            var vessel = PortData.Vessel(confirmed);
            var berth = _state.BerthOf(confirmed);
            Title = $"Berth {berth} confirmed for {vessel.Name}";
            Meta = $"Confirmed {_confirmedAt} · arrives {BerthFacts.Clock(vessel.Eta)}";
        }
        else if (pending is { } id)
        {
            var vessel = PortData.Vessel(id);
            var berthNumber = _state.BerthOf(id);
            var berth = PortData.Berths.First(b => b.Number == berthNumber);
            Title = $"{vessel.Name} needs berth {berth.Number} confirmed";
            Meta = _error ?? $"Arrives {BerthFacts.Clock(vessel.Eta)} · under-keel {BerthFacts.UnderKeel(vessel, berth):0.0} m · pilot aboard";
            ConfirmLabel = _busy ? "Confirming" : $"Confirm berth {berth.Number}";
        }
        else
        {
            Title = "Nothing needs you";
            Meta = "Every arrival has a confirmed berth";
        }

        Ground = done ? Tokens.Brush("SurfaceInvariantBrush") : Tokens.Brush("AmberInvariantBrush", .16);
        Edge = done ? Tokens.Brush("HairlineStrongInvariantBrush") : Tokens.Brush("InkInvariantBrush", 0);
        MetaForeground = _error is not null && !done ? Tokens.Brush("AlertInvariantBrush") : Tokens.Brush("TextMutedInvariantBrush");
        NeedsMark = done ? Visibility.Collapsed : Visibility.Visible;
        DoneMark = done ? Visibility.Visible : Visibility.Collapsed;
        ReviewVisibility = Subject is null ? Visibility.Collapsed : Visibility.Visible;
        ConfirmVisibility = done ? Visibility.Collapsed : Visibility.Visible;
        UndoVisibility = _confirmed is null ? Visibility.Collapsed : Visibility.Visible;
        IsBusy = _busy;
        OnPropertyChanged(nameof(Subject));
    }
}
