using Cargo.Controls;

namespace Cargo.Presentation;

public sealed partial class FleetView : Page
{
    public FleetView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    public FleetViewModel? ViewModel => DataContext as FleetViewModel;

    // xaml-lint: allow codebehind - the river map is a code-built scene that takes the store directly
    private void Attach()
    {
        if (ViewModel is not { } vm || RiverHost.Content is not null)
        {
            return;
        }

        RiverHost.Content = new RiverMap(vm.State);
        Bindings.Update();
    }
}
