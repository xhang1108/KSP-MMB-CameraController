Add-Type -Path "D:\Games\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll"

# Check AxisBinding members
$ab = [AxisBinding]
Write-Host "=== AxisBinding Methods ==="
foreach ($m in $ab.GetMethods([System.Reflection.BindingFlags]"Public,Instance,DeclaredOnly")) { 
    Write-Host "  $($m.ReturnType.Name) $($m.Name)($($m.GetParameters() | ForEach-Object { $_.ParameterType.Name }))" 
}

# Check AxisBinding_Single 
$abs = [AxisBinding_Single]
Write-Host "`n=== AxisBinding_Single Fields ==="
foreach ($f in $abs.GetFields([System.Reflection.BindingFlags]"Public,NonPublic,Instance")) { 
    Write-Host "  $($f.FieldType.Name) $($f.Name)" 
}
Write-Host "`n=== AxisBinding_Single Properties ==="
foreach ($p in $abs.GetProperties()) { Write-Host "  $($p.PropertyType.Name) $($p.Name)" }

# Also check SCROLL_VIEW_UP/DOWN type
$svup = [GameSettings].GetField("SCROLL_VIEW_UP")
Write-Host "`n=== SCROLL_VIEW_UP Type: $($svup.FieldType.FullName) ==="
$zmMod = [GameSettings].GetField("Editor_zoomScrollModifier")
Write-Host "=== Editor_zoomScrollModifier Type: $($zmMod.FieldType.FullName) ==="
