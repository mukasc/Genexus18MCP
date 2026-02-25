param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$TypeName,

    [Parameter(Mandatory=$true, Position=1)]
    [string]$MethodName,

    [string]$DllName = "Artech.Genexus.Common.dll"
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
    $type = $asm.GetTypes() | Where-Object { $_.FullName -like "*$TypeName*" -or $_.Name -eq $TypeName } | Select-Object -First 1
}

if (-not $type) {
    Write-Error "Type '$TypeName' not found in $DllName"
    return
}

Write-Host "`n=== Inspecting Method: $($type.FullName).$MethodName ===" -ForegroundColor Cyan

$methods = $type.GetMethods([Reflection.BindingFlags]::Public -bor [Reflection.BindingFlags]::Instance -bor [Reflection.BindingFlags]::Static -bor [Reflection.BindingFlags]::NonPublic) | 
           Where-Object { $_.Name -eq $MethodName }

if ($methods.Count -eq 0) {
    Write-Host "Method '$MethodName' not found on type '$($type.FullName)'." -ForegroundColor Red
    return
}

foreach ($m in $methods) {
    Write-Host "`nOverload:" -ForegroundColor Yellow
    Write-Host "  Return: $($m.ReturnType.FullName)"
    Write-Host "  Static: $($m.IsStatic)"
    Write-Host "  Virtual: $($m.IsVirtual)"
    
    $params = $m.GetParameters()
    if ($params.Count -eq 0) {
        Write-Host "  Parameters: (none)"
    } else {
        Write-Host "  Parameters:"
        foreach ($p in $params) {
            $defaultInfo = ""
            if ($p.HasDefaultValue) { $defaultInfo = " = $($p.DefaultValue)" }
            Write-Host "    - $($p.ParameterType.FullName) $($p.Name)$defaultInfo"
        }
    }
}
