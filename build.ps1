$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$dlls = Get-ChildItem -Recurse *.dll | Where-Object { $_.Name -ne "BlenderCameraControls.dll" } | Group-Object Name | ForEach-Object { $_.Group[0].FullName }
$cscParams = @("/t:library", "/out:BlenderCameraControls.dll")
foreach ($dll in $dlls) { $cscParams += "/r:$dll" }
Get-ChildItem *.cs | Where-Object { $_.Name -notlike "Old*" } | ForEach-Object { $cscParams += $_.FullName }
& $csc $cscParams

# Deployment
$targetDir = "D:\Games\steamapps\common\Kerbal Space Program\GameData\BlenderCameraControl"
if (Test-Path $targetDir) {
    Copy-Item "BlenderCameraControls.dll" -Destination $targetDir -Force
    Write-Host "Deployed to $targetDir"
} else {
    Write-Warning "Target directory $targetDir not found. Manual copy required."
}
