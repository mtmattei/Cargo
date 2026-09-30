using Cargo.Controls;

namespace Cargo.Presentation;

public sealed partial class DockingView : UserControl
{
    private bool _attached;

    public DockingView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    public DockingViewModel? ViewModel => DataContext as DockingViewModel;

    // xaml-lint: allow codebehind - the berth map is a code-built drag scene over the store; its hint
    // feeds the model, and the model's refreshes run only while the plan is on screen
    private void Attach()
    {
        if (!_attached && ViewModel is { } vm)
        {
            _attached = true;
            var map = new BerthMap(vm.State);
            map.HintChanged += (_, message) => vm.Hint = message;
            MapHost.Content = map;
            this.RebuildWhenVisible(vm.State, vm.Refresh);
            this.TickWhenVisible(vm.State, vm.Tick);
        }

        Bindings.Update();
    }
}
