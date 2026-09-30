namespace Cargo.Presentation;

/// <summary>Cargo: the container explorer above the yard it lives in.</summary>
public sealed partial class CargoView : Page
{
    public CargoView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public CargoViewModel? ViewModel => DataContext as CargoViewModel;
}
