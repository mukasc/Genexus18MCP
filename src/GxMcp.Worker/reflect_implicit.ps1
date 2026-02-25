$asm1 = [Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\GeneXus\GeneXus18\Artech.Architecture.Common.dll")
$asm2 = [Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\GeneXus\GeneXus18\Artech.Genexus.Common.dll")

try {
    # We can't easily instantiate Attribute without a Model, so we find an existing object.
    # We don't have a model in context easily. 
    # Can we just use static methods on Formula? 
    $formulaType = $asm2.GetType("Artech.Genexus.Common.Objects.Formula")
    # Let's check if there is an implicit cast from string to Formula? No, C# doesn't show implicit casts via GetConstructors usually but in GetMethods.
} catch { }

$formulaType.GetMethods() | Where-Object Name -like 'op_Implicit' | Select-Object -ExpandProperty ToString | Out-File 'C:\Projetos\GenexusMCP\src\GxMcp.Worker\reflect_implicit.txt' -Encoding UTF8
