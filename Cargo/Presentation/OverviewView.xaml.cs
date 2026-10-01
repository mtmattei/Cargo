namespace Cargo.Presentation;

public sealed partial class OverviewView : Page
{
    public OverviewView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public OverviewViewModel? ViewModel => DataContext as OverviewViewModel;

    /// <summary>Where the first section (Today at North Quay) starts, in the coordinates of <paramref name="content"/>.</summary>
    public double FirstSectionTop(UIElement content) =>
        FirstSection.TransformToVisual(content).TransformPoint(new Windows.Foundation.Point(0, 0)).Y;

    /// <summary>A Next 6 h row's spoken name: "Nordic Star, arrives Berth 07, in 42 min".</summary>
    public static string SixName(string name, string sub, string countdown) => $"{name}, {sub}, {countdown}";

    // The timeline table's rows: future hours on the ahead-of-now shade, the NOW row between two ink rules

    public static Microsoft.UI.Xaml.Media.Brush RowGround(bool future) =>
        future ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AheadShadeInvariantBrush"] : Tokens.Transparent;

    public static Microsoft.UI.Xaml.Media.Brush RowRule(bool now) =>
        now ? Tokens.Brush("InkInvariantBrush") : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["HairlineInvariantBrush"];

    public static Thickness RowRuleWidth(bool now) => now ? new Thickness(0, 1.25, 0, 1.25) : new Thickness(0, 1, 0, 0);
}
