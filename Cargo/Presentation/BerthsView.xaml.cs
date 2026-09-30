using Cargo.Controls;

namespace Cargo.Presentation;

/// <summary>Berths: the decision waiting on you and every berth's status, above the plan and the vessel you are working.</summary>
public sealed partial class BerthsView : Page
{
    public BerthsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    private bool _attached;

    public BerthsViewModel? ViewModel => DataContext as BerthsViewModel;

    // xaml-lint: allow codebehind - the panels follow the store only while the page is on screen
    // (Live), like every other section; the page stays alive once visited
    private void Attach()
    {
        if (!_attached && ViewModel is { } vm)
        {
            _attached = true;
            this.RebuildWhenVisible(vm.State, vm.RefreshPanels);
            this.TickWhenVisible(vm.State, vm.RefreshPanels);
        }

        Bindings.Update();
    }
}
