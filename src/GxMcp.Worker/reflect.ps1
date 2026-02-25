$asm = [Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\GeneXus\GeneXus18\Artech.Genexus.Common.dll")

function Dump-Type($typeName) {
    $type = $asm.GetType($typeName)
    if (!$type) { return "Type $typeName not found" }
    
    $out = @()
    $out += "===== $typeName ====="
    
    $out += "-- Constructors --"
    $type.GetConstructors() | ForEach-Object { $out += $_.ToString() }
    
    $out += "-- Properties --"
    $type.GetProperties() | ForEach-Object { $out += $_.ToString() }
    
    $out += "-- Methods --"
    $type.GetMethods() | ForEach-Object { $out += $_.ToString() }
    
    return $out
}

$result = @()
$result += Dump-Type "Artech.Genexus.Common.Objects.Formula"
$result += Dump-Type "Artech.Genexus.Common.Objects.Attribute"
$result += Dump-Type "Artech.Genexus.Common.Objects.TransactionAttribute"

$result | Out-File -FilePath "C:\Projetos\GenexusMCP\src\GxMcp.Worker\reflect.txt" -Encoding UTF8
