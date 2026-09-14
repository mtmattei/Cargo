<#
.SYNOPSIS
  Captures a window of the running app to a PNG.

.DESCRIPTION
  PrintWindow with PW_RENDERFULLCONTENT is occlusion-proof, but on a Skia GL desktop
  window it can return TRUE and still hand back a blank bitmap. A success return is not
  evidence of pixels, so the bitmap is sampled and falls back to a screen copy when it
  comes back effectively uniform.

  Add-Type notes for .NET 10 / PowerShell 7: do not pass -UsingNamespace (Add-Type already
  emits it), and keep every System.Drawing type out of the C# string — those sit behind
  type-forwards Add-Type cannot resolve from source.
#>
[CmdletBinding()]
param(
    [string]$ProcessName = "Cargo",
    [Parameter(Mandatory = $true)][string]$OutPath
)

Add-Type -AssemblyName System.Drawing

if (-not ("Win32Capture" -as [type])) {
    Add-Type -Name Win32Capture -Namespace Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
'@
}

$proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne 0 } |
        Select-Object -First 1

if (-not $proc) { throw "No visible window for process '$ProcessName'." }

$hwnd = $proc.MainWindowHandle

# A minimised window reports a tiny off-screen rect, which captures as a 160x28 sliver.
if ([Native.Win32Capture]::IsIconic($hwnd)) {
    [void][Native.Win32Capture]::ShowWindow($hwnd, 9)   # SW_RESTORE
    Start-Sleep -Milliseconds 600
}

$rect = New-Object Native.Win32Capture+RECT
[void][Native.Win32Capture]::GetWindowRect($hwnd, [ref]$rect)

$width  = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
if ($width -le 0 -or $height -le 0) { throw "Window has no size ($width x $height)." }

$bitmap   = New-Object System.Drawing.Bitmap $width, $height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$hdc      = $graphics.GetHdc()

# PW_RENDERFULLCONTENT = 2
[void][Native.Win32Capture]::PrintWindow($hwnd, $hdc, 2)
$graphics.ReleaseHdc($hdc)
$graphics.Dispose()

# Sample the result: a uniform bitmap means PrintWindow lied about succeeding.
$samples = @()
foreach ($fx in 0.2, 0.5, 0.8) {
    foreach ($fy in 0.2, 0.5, 0.8) {
        $samples += $bitmap.GetPixel([int]($width * $fx), [int]($height * $fy)).ToArgb()
    }
}

if (($samples | Select-Object -Unique).Count -le 1) {
    Write-Warning "PrintWindow returned a uniform bitmap; falling back to a screen copy."
    $bitmap.Dispose()

    [void][Native.Win32Capture]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 400

    $bitmap   = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
    $graphics.Dispose()
}

$bitmap.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()

Write-Output "Captured ${width}x${height} to $OutPath"

