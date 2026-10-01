namespace Cargo.Presentation;

public sealed partial class SeaWeatherPanel : UserControl
{
    public SeaWeatherPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public PortState? ViewModel => DataContext as PortState;
}
