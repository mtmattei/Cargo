using Cargo.Controls;

namespace Cargo.Presentation;

public sealed partial class VesselsView : UserControl
{
    private bool _attached;

    public VesselsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    public VesselsViewModel? ViewModel => DataContext as VesselsViewModel;

    // xaml-lint: allow codebehind - the vessel profile is a code-built scene over the store, and the
    // model's refreshes run only while this panel is on screen (Live gates on the view's visibility)
    private void Attach()
    {
        if (!_attached && ViewModel is { } vm)
        {
            _attached = true;
            ProfileHost.Content = new VesselProfile(vm.State);
            this.RebuildWhenVisible(vm.State, vm.Refresh);
            this.TickWhenVisible(vm.State, vm.RefreshLive);
        }

        Bindings.Update();
    }
}
