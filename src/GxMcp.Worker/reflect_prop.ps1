$asm = [Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\GeneXus\GeneXus18\Artech.Common.Properties.dll")

function Dump-Type($typeName) {
    try {
        $type = $asm.GetType($typeName)
    } catch { return "Error loading $typeName" }
    
    if (!$type) { return "Type $typeName not found" }
    
    $out = @()
    $out += "===== $typeName ====="
    
    $out += "-- Properties --"
    $type.GetProperties() | ForEach-Object { $out += $_.ToString() }
    
    $out += "-- Methods --"
    $type.GetMethods() | ForEach-Object { $out += $_.ToString() }
    
    return $out
}

$result = @()
$result += Dump-Type "Artech.Common.Properties.Property"
$result | Out-File -FilePath "C:\Projetos\GenexusMCP\src\GxMcp.Worker\reflect_prop.txt" -Encoding UTF8
