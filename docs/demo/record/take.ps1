param([string]$Out = "C:\Users\Platform006\Videos\Cargo\overview-walkthrough-v2-raw.mp4")
. "$PSScriptRoot\drive.ps1"

# Coordinates are the maximized client (1920x1129 at 0,23 on the 1920x1200 display).
Get-Process Cargo -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800

# Recording first: the maximized client area (1128 high, even for H.264)
$ff = New-Object System.Diagnostics.Process
$ff.StartInfo.FileName = "ffmpeg"
$ff.StartInfo.Arguments = "-hide_banner -loglevel error -y -f lavfi -i ddagrab=output_idx=0:framerate=30:offset_x=0:offset_y=23:video_size=1920x1128:draw_mouse=1 -vf hwdownload,format=bgra -c:v libx264 -preset veryfast -pix_fmt yuv420p -crf 16 `"$Out`""
$ff.StartInfo.UseShellExecute = $false
$ff.StartInfo.RedirectStandardInput = $true
$ff.Start() | Out-Null
$clock = [Diagnostics.Stopwatch]::StartNew()
Start-Sleep -Milliseconds 1000
"recording live"

$h = Start-CargoMaximized
"maximized at {0:N1} s" -f $clock.Elapsed.TotalSeconds
[U]::SetCursorPos($script:Origin.X + 1700, $script:Origin.Y + 1000) | Out-Null

# 1. Arrive (the page rebuilds twice while maximizing during startup; it is stable about 7 s in)
Wait 8500
Move-To 1754 117 700; Wait 1400            # the duty card lifts
Move-To 1520 105 500; Wait 1100            # Needs you 4

# 2. Read the harbour
Move-To 1375 345 600; Wait 1300            # Nordic Star, pilot aboard
Click 96 478 500; Wait 600                 # camera menu
Click 104 353 350; Wait 1900               # From sea
Click 96 478 400; Wait 600
Click 104 317 350; Wait 1600               # Overview
Click 902 478 500; Wait 1300               # Yard
Click 996 478 300; Wait 1300               # Security
Click 1097 478 300; Wait 1300              # Traffic
Click 820 478 400; Wait 1000               # Map
Click 1526 478 500; Wait 1800              # Key
Key 0x1B; Wait 700
Click 1830 212 600; Wait 1800              # Expand
Click 1830 212 250; Wait 1500              # Collapse
Drag 900 430 1150 410 1200; Wait 1000      # orbit by hand
Click 90 478 500; Wait 600
Click 104 317 350; Wait 1600               # back to Overview
Click-Tag; Wait 2600                       # select Nordic Star: fly-to + facts card
Click-Tag; Wait 1000                       # close the card
Click 90 478 500; Wait 600
Click 104 317 350; Wait 1600               # Overview again

# 3. Read the day
Move-To 908 750 700; Wait 1600             # hover Nordic Star on the timeline: its tag lights
Move-To 904 767 300; Wait 1200             # Baltic Crown
Click 1113 596 500; Wait 1800              # Table
Click 1041 596 250; Wait 1000              # Chart

# 4. Act
Click 1554 875 700; Wait 2400              # Confirm berth: 4 -> 3, the tag turns light
Move-To 1420 650 500; Wait 1500            # the rest stay queued, timed

# 5. Scroll: the masthead folds, the harbour condenses to its strip
Click 389 583 600; Wait 300                # focus the page (the timeline heading, not a control)
Key 0x22; Wait 2200                        # PageDown: Next 6 h and Activity under the mini band
Move-To 1340 340 600; Wait 1800            # hover Nordic Star in Next 6 h: its tag lights in the strip
Move-To 1340 410 300; Wait 1200            # Kaida Maru
Click 500 785 600; Wait 2600               # open Driver ID pending: lift, blur back, detail
Click 500 784 250; Wait 1500               # close: the list returns to default
Click 1812 91 700; Wait 2500               # Show harbour: back to the top, full band
Move-To 1700 1000 600; Wait 1500           # end hold

$ff.StandardInput.Write("q"); $ff.StandardInput.Flush()
$ff.WaitForExit(15000) | Out-Null
"stopped at {0:N1} s" -f $clock.Elapsed.TotalSeconds
