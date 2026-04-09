$cmdArgs = @("/target:library", "/out:BlenderCameraControls.dll")
Get-ChildItem -Filter *.dll | Where-Object { $_.Name -notmatch "BlenderCamera" } | ForEach-Object { $cmdArgs += "/reference:" + $_.Name }
$cmdArgs += "BlenderCameraControls.cs"
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" $cmdArgs
