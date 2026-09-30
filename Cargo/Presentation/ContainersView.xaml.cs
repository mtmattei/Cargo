using Cargo.Controls;

namespace Cargo.Presentation;

public sealed partial class ContainersView : UserControl
{
    private bool _attached;

    public ContainersView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    public ContainersViewModel? ViewModel => DataContext as ContainersViewModel;

    // xaml-lint: allow codebehind - the model's refresh runs only while the explorer is on screen
    private void Attach()
    {
        if (!_attached && ViewModel is { } vm)
        {
            _attached = true;
            this.RebuildWhenVisible(vm.State, vm.Refresh);
        }

        Bindings.Update();
    }
}
