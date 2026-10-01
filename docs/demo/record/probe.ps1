. "$PSScriptRoot\drive.ps1"
$h = Start-CargoMaximized
Start-Sleep -Seconds 7
Use-Window $h
Click 1554 875 300; Start-Sleep -Milliseconds 1500
Click 389 583 300; Start-Sleep -Milliseconds 300
Key 0x22; Start-Sleep -Milliseconds 1500; Snap "m_pgdn"
Key 0x23; Start-Sleep -Milliseconds 1500; Snap "m_end"
