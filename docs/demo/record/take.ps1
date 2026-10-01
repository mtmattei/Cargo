param([string]$Out = "C:\Users\Platform006\Videos\Cargo\overview-walkthrough-take2-raw.mp4")
. "$PSScriptRoot\drive.ps1"

Get-Process Cargo -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800

# 1. Recording first
$ff = New-Object System.Diagnostics.Process
$ff.StartInfo.FileName = "ffmpeg"
$ff.StartInfo.Arguments = "-hide_banner -loglevel error -y -f lavfi -i ddagrab=output_idx=0:framerate=30:offset_x=8:offset_y=31:video_size=1664x980:draw_mouse=1 -vf hwdownload,format=bgra -c:v libx264 -preset veryfast -pix_fmt yuv420p -crf 16 `"$Out`""
$ff.StartInfo.UseShellExecute = $false
$ff.StartInfo.RedirectStandardInput = $true
$ff.Start() | Out-Null
$clock = [Diagnostics.Stopwatch]::StartNew()
Start-Sleep -Milliseconds 1200
"recording live"

# 2. Fresh app, pinned at the recorded rect the moment its window exists
$env:APP_NO_HOTDESIGN = '1'
Start-Process "C:\Users\Platform006\Cargo\Cargo\bin\Debug\net10.0-desktop\Cargo.exe"
$h = [IntPtr]::Zero
while ($h -eq [IntPtr]::Zero) {
    Start-Sleep -Milliseconds 30
    $p = Get-Process Cargo -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if ($p) { $h = $p.MainWindowHandle }
}
[U]::SetWindowPos($h, [IntPtr](-1), 0, 0, 0, 0, 0x0001 -bor 0x0040) | Out-Null
[U]::SetForegroundWindow($h) | Out-Null
Use-Window $h
"pinned at {0:N1} s" -f $clock.Elapsed.TotalSeconds
[U]::SetCursorPos($script:Origin.X + 1500, $script:Origin.Y + 940) | Out-Null

# 1. Arrive
Wait 5500                                   # intro: greeting, harbour, the duty card drops in
Move-To 1497 117 900; Wait 2200             # hover the duty card: it lifts
Move-To 1270 118 700; Wait 1800             # the figures: 3, 2, Needs you 4

# 2. Read the harbour
Move-To 1246 346 800; Wait 2200             # Nordic Star, pilot aboard, 42 min
Click 96 478; Wait 900                      # camera menu
Click 104 353 500; Wait 2800                # From sea
Click 96 478; Wait 900
Click 104 317 500; Wait 2400                # Overview
Click 775 478; Wait 2000                    # Yard
Click 868 478; Wait 2000                    # Security
Click 968 478; Wait 2000                    # Traffic
Click 690 478; Wait 1600                    # Map
Click 1270 478 700; Wait 2800               # Key
Key 0x1B; Wait 1000
Click 1574 212 800; Wait 2800               # Expand
Click 1572 212 300; Wait 2200               # Collapse
Drag 640 430 900 400 1600; Wait 1600        # orbit by hand
Click 90 478 700; Wait 900
Click 104 317 500; Wait 2400                # back to Overview
Click-Tag; Wait 3800                        # select Nordic Star: fly-to + facts card
Click-Tag; Wait 1400                        # close the card (found wherever the fly-to put it)
Click 90 478 700; Wait 900
Click 104 317 500; Wait 2400                # Overview again

# 3. Read the day
Move-To 781 750 900; Wait 2400              # hover Nordic Star on the timeline: its tag lights
Move-To 776 767 400; Wait 1800              # Baltic Crown
Click 985 596 600; Wait 2800                # Table
Click 913 596 300; Wait 1600                # Chart

# 4. Act
Click 1425 875 900; Wait 3400               # Confirm berth: 4 -> 3, tag turns light
Move-To 1290 650 700; Wait 2600             # the rest stay queued, timed

# 5. Look back
Click 260 583 700; Wait 400                  # focus the page (the timeline heading, not a control)
Key 0x22; Wait 3000                          # PageDown: Next 6 h, ships to scale
Key 0x23; Wait 2000                          # End: Activity
Click 400 637 700; Wait 3800                # open Driver ID pending: lift, blur back, detail
Click 400 636 300; Wait 2200                # close: the list returns to default
Move-To 1500 940 900; Wait 2500             # end hold

# Stop
$ff.StandardInput.Write("q"); $ff.StandardInput.Flush()
$ff.WaitForExit(15000) | Out-Null
"stopped at {0:N1} s" -f $clock.Elapsed.TotalSeconds
