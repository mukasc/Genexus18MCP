param(
    [string]$Action = "DumpAttributeProps"
)

$gxPath = "C:\Program Files (x86)\GeneXus\GeneXus18"
$commonDll = [Reflection.Assembly]::LoadFrom("$gxPath\Artech.Genexus.Common.dll")
$archDll = [Reflection.Assembly]::LoadFrom("$gxPath\Artech.Architecture.Common.dll")
$frameDll = [Reflection.Assembly]::LoadFrom("$gxPath\Artech.FrameworkDE.dll")

Write-Host "--- GeneXus SDK Behavior Verification ---" -ForegroundColor Cyan

switch ($Action) {
    "DumpAttributeProps" {
        $t = $commonDll.GetType("Artech.Genexus.Common.Objects.Attribute")
        Write-Host "Inspecting Attribute properties..."
        $t.GetProperties() | Select-Object Name, PropertyType | Format-Table -AutoSize
    }
    "CheckPropertyCollection" {
        $t = $commonDll.GetType("Artech.Genexus.Common.Properties.PropertyCollection")
        if (-not $t) { $t = $archDll.GetType("Artech.Architecture.Common.Objects.Properties.PropertyCollection") }
        Write-Host "Inspecting PropertyCollection methods..."
        $t.GetMethods() | Where-Object { $_.Name -match "Get" } | Select-Object Name, ReturnType | Format-Table -AutoSize
    }
    Default {
        Write-Host "Unknown action: $Action" -ForegroundColor Red
        Write-Host "Add your custom logic to this switch block in verify_sdk_behavior.ps1"
    }
}
