using Cargo.Controls;

namespace Cargo.Presentation;

public sealed partial class YardView : UserControl
{
    private bool _attached;

    public YardView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    public YardViewModel? ViewModel => DataContext as YardViewModel;

    // xaml-lint: allow codebehind - the yard map is a code-built scene over the store, and the
    // model's refreshes run only while the yard is on screen
    private void Attach()
    {
        if (!_attached && ViewModel is { } vm)
        {
            _attached = true;
            MapHost.Content = new YardMap(vm.State);
            this.RebuildWhenVisible(vm.State, vm.Refresh);
            this.RebuildOnYardMove(vm.State, vm.Refresh);
        }

        Bindings.Update();
    }
}
