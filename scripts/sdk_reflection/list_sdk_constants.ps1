param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$TypeName,

    [string]$DllName = "Artech.Genexus.Common.dll",
    [string]$Filter = ""
)

$gxPath = "C:\Program Files (x86)\GeneXus\GeneXus18"
$dllPath = Join-Path $gxPath $DllName

if (-not (Test-Path $dllPath)) {
    Write-Error "DLL not found: $dllPath"
    return
}

try {
    $asm = [Reflection.Assembly]::LoadFrom($dllPath)
} catch {
    Write-Error "Failed to load assembly: $_"
    return
}

$type = $asm.GetType($TypeName)
if (-not $type) {
    # Try fuzzy match
    $type = $asm.GetTypes() | Where-Object { $_.FullName -like "*$TypeName*" -or $_.Name -eq $TypeName } | Select-Object -First 1
}

if (-not $type) {
    Write-Error "Type '$TypeName' not found in $DllName"
    return
}

Write-Host "`n=== Constants for $($type.FullName) ===" -ForegroundColor Cyan

if ($type.IsEnum) {
    [Enum]::GetNames($type) | ForEach-Object {
        if (-not $Filter -or $_ -like "*$Filter*") {
            [PSCustomObject]@{ Name = $_; Value = [int][Enum]::Parse($type, $_) }
        }
    } | Sort-Object Value | Format-Table -AutoSize
} else {
    $type.GetFields([Reflection.BindingFlags]::Public -bor [Reflection.BindingFlags]::Static) | 
    Where-Object { -not $Filter -or $_.Name -like "*$Filter*" } | 
    Select-Object Name, @{Name="Value";Expression={ try { $_.GetValue($null) } catch { "Error" } }} | 
    Sort-Object Name | Format-Table -AutoSize
}
