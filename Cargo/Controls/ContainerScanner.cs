using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace Cargo.Controls;

/// <summary>
/// The 3D inspection view. WinUI has no CSS-style 3D transforms, so the box is projected
/// by hand: eight corners rotated about Y then X, divided through by a 1500-unit
/// perspective, and the six faces painted back to front. X-ray drops the shell to a
/// wireframe and lights the manifest blocks inside.
/// </summary>
public sealed partial class ContainerScanner : Panel
{
    private const double Length = 520;
    private const double BoxHeight = 220;
    private const double Depth = 210;
    private const double Perspective = 1500;

    private readonly Canvas _canvas = new();

    private PortState? _state;
    private Point _dragOrigin;
    private double _dragRx;
    private double _dragRy;
    private bool _dragging;

    public ContainerScanner()
    {
        Children.Add(_canvas);
        Background = Tokens.Transparent;

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += (_, e) =>
        {
            _dragging = false;
            ReleasePointerCapture(e.Pointer);
        };
        PointerCaptureLost += (_, _) => _dragging = false;

        SizeChanged += (_, _) => Render();
    }

    public PortState? State
    {
        get => _state;
        set
        {
            if (_state is not null)
            {
                _state.StructureChanged -= OnChanged;
                _state.PropertyChanged -= OnPropertyChanged;
            }

            _state = value;

            if (_state is not null)
            {
                _state.StructureChanged += OnChanged;
                _state.PropertyChanged += OnPropertyChanged;
            }

            Render();
        }
    }

    private void OnChanged(object? sender, EventArgs e) => Render();

    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PortState.ScanRotationX) or nameof(PortState.ScanRotationY)
            or nameof(PortState.ScanZoom) or nameof(PortState.Xray))
        {
            Render();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _canvas.Measure(availableSize);
        return new Size(
            double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 480 : availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _canvas.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
        return finalSize;
    }

    // ── Projection ────────────────────────────────────────────────────────────

    private Point Project(double x, double y, double z, double centreX, double centreY, double zoom)
    {
        var state = _state!;
        var ry = state.ScanRotationY * Math.PI / 180;
        var rx = state.ScanRotationX * Math.PI / 180;

        // Yaw about the vertical, then pitch towards the viewer
        var x1 = x * Math.Cos(ry) + z * Math.Sin(ry);
        var z1 = -x * Math.Sin(ry) + z * Math.Cos(ry);
        var y2 = y * Math.Cos(rx) - z1 * Math.Sin(rx);
        var z2 = y * Math.Sin(rx) + z1 * Math.Cos(rx);

        var scale = zoom * Perspective / (Perspective + z2);
        return new Point(centreX + x1 * scale, centreY + y2 * scale);
    }

    private double DepthOf(double x, double y, double z)
    {
        var state = _state!;
        var ry = state.ScanRotationY * Math.PI / 180;
        var rx = state.ScanRotationX * Math.PI / 180;

        var z1 = -x * Math.Sin(ry) + z * Math.Cos(ry);
        return y * Math.Sin(rx) + z1 * Math.Cos(rx);
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    private void Render()
    {
        _canvas.Children.Clear();

        if (_state is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var container = _state.SelectedContainer;
        var tint = Tokens.Color(container.TintToken);
        var xray = _state.Xray;
        var zoom = _state.ScanZoom;

        var cx = ActualWidth / 2;
        var cy = ActualHeight / 2;

        var hx = Length / 2;
        var hy = BoxHeight / 2;
        var hz = Depth / 2;

        // Six faces of the box, each with its own shading
        var faces = new (double[] Xs, double[] Ys, double[] Zs, Color Fill)[]
        {
            (new[] { -hx, hx, hx, -hx }, new[] { -hy, -hy, hy, hy }, new[] { hz, hz, hz, hz }, tint),
            (new[] { hx, -hx, -hx, hx }, new[] { -hy, -hy, hy, hy }, new[] { -hz, -hz, -hz, -hz }, Tokens.Shade(tint, -18)),
            (new[] { hx, hx, hx, hx }, new[] { -hy, -hy, hy, hy }, new[] { hz, -hz, -hz, hz }, Tokens.Shade(tint, -46)),
            (new[] { -hx, -hx, -hx, -hx }, new[] { -hy, -hy, hy, hy }, new[] { -hz, hz, hz, -hz }, Tokens.Shade(tint, -46)),
            (new[] { -hx, hx, hx, -hx }, new[] { -hy, -hy, -hy, -hy }, new[] { -hz, -hz, hz, hz }, Tokens.Shade(tint, 30)),
            (new[] { -hx, hx, hx, -hx }, new[] { hy, hy, hy, hy }, new[] { hz, hz, -hz, -hz }, Tokens.Shade(tint, -70))
        };

        var edge = xray ? Tokens.Brush("TealBrightColor", 0.75) : Tokens.Brush("InkColor", 0.35);

        var ordered = faces
            .Select(face => (Face: face, Depth: Enumerable.Range(0, 4)
                .Select(i => DepthOf(face.Xs[i], face.Ys[i], face.Zs[i])).Average()))
            .OrderByDescending(entry => entry.Depth)
            .ToList();

        foreach (var (face, _) in ordered)
        {
            var polygon = new Polygon
            {
                Fill = Tokens.Of(face.Fill, xray ? 0.14 : 1),
                Stroke = edge,
                StrokeThickness = 1
            };

            for (var i = 0; i < 4; i++)
            {
                polygon.Points.Add(Project(face.Xs[i], face.Ys[i], face.Zs[i], cx, cy, zoom));
            }

            _canvas.Children.Add(polygon);
        }

        if (xray)
        {
            RenderCargo(container, cx, cy, zoom, hx, hy);
        }
    }

    private void RenderCargo(ContainerDef container, double cx, double cy, double zoom, double hx, double hy)
    {
        var manifest = PortData.Manifests.TryGetValue(container.Id, out var listed)
            ? listed
            : PortData.DefaultManifest;

        var blocks = manifest
            .Select((item, i) =>
            {
                var seed = i * 37 + container.Id.Length;
                var width = 60 + Math.Round(Geo.H(seed) * 50);
                var height = 90 + Math.Round(Geo.H(seed + 3) * 90);
                var x = -hx + 14 + i * Math.Floor(490.0 / manifest.Count);
                var z = -60 + Math.Round(Geo.H(seed + 7) * 120);
                return (Item: item, X: x, W: width, H: height, Z: z);
            })
            .OrderBy(b => DepthOf(b.X + b.W / 2, hy - b.H / 2, b.Z))
            .ToList();

        const double blockDepth = 52;

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var color = Tokens.Color(block.Item.TintToken);
            var top = hy - block.H;
            var front = block.Z + blockDepth / 2;
            var back = block.Z - blockDepth / 2;

            // Front, top and right faces give each mass enough solidity to read as cargo
            AddFace(new[]
            {
                (block.X, top, front), (block.X + block.W, top, front),
                (block.X + block.W, hy, front), (block.X, hy, front)
            }, Tokens.Of(color, 0.85), cx, cy, zoom);

            AddFace(new[]
            {
                (block.X, top, back), (block.X + block.W, top, back),
                (block.X + block.W, top, front), (block.X, top, front)
            }, Tokens.Of(Tokens.Shade(color, 34), 0.8), cx, cy, zoom);

            AddFace(new[]
            {
                (block.X + block.W, top, back), (block.X + block.W, top, front),
                (block.X + block.W, hy, front), (block.X + block.W, hy, back)
            }, Tokens.Of(Tokens.Shade(color, -40), 0.8), cx, cy, zoom);

            // Labels alternate above and below so neighbouring masses stay readable
            var label = new TextBlock
            {
                Text = block.Item.Name.Split(" · ")[0],
                FontFamily = (FontFamily)Application.Current.Resources["MonoMediumFont"],
                FontSize = 9,
                Foreground = Tokens.Brush("SurfaceBrush")
            };
            var anchor = Project(block.X + 4, hy - 6, front, cx, cy, zoom);
            Canvas.SetLeft(label, anchor.X);
            Canvas.SetTop(label, anchor.Y - (i % 2 == 0 ? 14 : 2));
            _canvas.Children.Add(label);
        }
    }

    private void AddFace(IReadOnlyList<(double X, double Y, double Z)> corners, Brush fill,
        double cx, double cy, double zoom)
    {
        var polygon = new Polygon
        {
            Fill = fill,
            Stroke = Tokens.Brush("DeckWhiteColor", 0.4),
            StrokeThickness = 1
        };

        foreach (var (x, y, z) in corners)
        {
            polygon.Points.Add(Project(x, y, z, cx, cy, zoom));
        }

        _canvas.Children.Add(polygon);
    }

    // ── Drag to rotate ────────────────────────────────────────────────────────

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_state is null)
        {
            return;
        }

        _dragging = true;
        _dragOrigin = e.GetCurrentPoint(this).Position;
        _dragRx = _state.ScanRotationX;
        _dragRy = _state.ScanRotationY;
        CapturePointer(e.Pointer);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging || _state is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this).Position;
        _state.ScanRotationY = _dragRy + (point.X - _dragOrigin.X) * 0.4;
        _state.ScanRotationX = Math.Clamp(_dragRx - (point.Y - _dragOrigin.Y) * 0.4, -90, 90);
    }
}
