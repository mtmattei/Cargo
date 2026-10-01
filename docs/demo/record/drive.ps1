# Real OS input for the Cargo window, in client coordinates (the rehearsal's MCP coordinates).
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class U {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, int d, IntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint f, IntPtr e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  public struct POINT { public int X, Y; }
  public struct RECT { public int L, T, R, B; }
}
"@
[U]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null

function Get-CargoWindow {
    (Get-Process Cargo -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1).MainWindowHandle
}

$script:H = [IntPtr]::Zero
$script:Origin = $null

function Use-Window([IntPtr]$h) {
    $script:H = $h
    $p = New-Object U+POINT
    [U]::ClientToScreen($h, [ref]$p) | Out-Null
    $script:Origin = $p
}

function Get-ClientRect {
    $r = New-Object U+RECT
    [U]::GetClientRect($script:H, [ref]$r) | Out-Null
    [pscustomobject]@{ X = $script:Origin.X; Y = $script:Origin.Y; W = $r.R; H = $r.B }
}

function Move-To([int]$x, [int]$y, [int]$ms = 450) {
    $from = New-Object U+POINT
    [U]::GetCursorPos([ref]$from) | Out-Null
    $tx = $script:Origin.X + $x; $ty = $script:Origin.Y + $y
    $steps = [Math]::Max(1, [int]($ms / 12))
    for ($i = 1; $i -le $steps; $i++) {
        $t = $i / $steps
        $e = 1 - [Math]::Pow(1 - $t, 3)
        [U]::SetCursorPos([int]($from.X + ($tx - $from.X) * $e), [int]($from.Y + ($ty - $from.Y) * $e)) | Out-Null
        Start-Sleep -Milliseconds 12
    }
}

function Click([int]$x, [int]$y, [int]$ms = 450) {
    Move-To $x $y $ms
    Start-Sleep -Milliseconds 120
    [U]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [U]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
}

function Wheel([int]$notches, [int]$gapMs = 140) {
    $dir = if ($notches -lt 0) { -120 } else { 120 }
    for ($i = 0; $i -lt [Math]::Abs($notches); $i++) {
        [U]::mouse_event(0x0800, 0, 0, $dir, [IntPtr]::Zero)
        Start-Sleep -Milliseconds $gapMs
    }
}

function Drag([int]$x1, [int]$y1, [int]$x2, [int]$y2, [int]$ms = 1400) {
    Move-To $x1 $y1 400
    Start-Sleep -Milliseconds 150
    [U]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
    Move-To $x2 $y2 $ms
    Start-Sleep -Milliseconds 100
    [U]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
}

function Key([byte]$vk) {
    [U]::keybd_event($vk, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 40
    [U]::keybd_event($vk, 0, 2, [IntPtr]::Zero)
}

function Wait([int]$ms) { Start-Sleep -Milliseconds $ms }

Add-Type -AssemblyName System.Drawing
# Centre of the near-black block in a band of the client area (the Nordic Star tag in the harbour band)
function Find-DarkTag([int]$yMin = 185, [int]$yMax = 445) {
    $r = Get-ClientRect
    $bmp = New-Object System.Drawing.Bitmap $r.W, ($yMax - $yMin)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.X, $r.Y + $yMin, 0, 0, $bmp.Size)
    $g.Dispose()
    $xs = New-Object System.Collections.Generic.List[int]; $ys = New-Object System.Collections.Generic.List[int]
    for ($y = 0; $y -lt $bmp.Height; $y += 3) {
        for ($x = 0; $x -lt $bmp.Width; $x += 3) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.R -lt 45 -and $c.G -lt 45 -and $c.B -lt 50) { $xs.Add($x); $ys.Add($y) }
        }
    }
    $bmp.Dispose()
    if ($xs.Count -lt 150) { return $null }
    $xs.Sort(); $ys.Sort()
    [pscustomobject]@{ X = $xs[[int]($xs.Count / 2)]; Y = $ys[[int]($ys.Count / 2)] + $yMin; N = $xs.Count }
}

function Click-Tag {
    $t = Find-DarkTag
    if ($t) { Click $t.X $t.Y 700; "tag at $($t.X),$($t.Y) ($($t.N))" } else { "tag not found" }
}
