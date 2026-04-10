$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$dlls = Get-ChildItem -Recurse *.dll | Where { $_.Name -ne "BlenderCameraControls.dll" } | Group-Object Name | ForEach-Object { $_.Group[0].FullName }
$args = @("/t:library", "/out:BlenderCameraControls.dll")
foreach ($dll in $dlls) { $args += "/r:$dll" }
$args += "BlenderCameraControls.cs"
& $csc $args
