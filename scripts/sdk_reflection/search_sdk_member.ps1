param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$Query,

    [string[]]$Dlls = @("Artech.Genexus.Common.dll", "Artech.Architecture.Common.dll", "Artech.Common.dll", "Artech.FrameworkDE.dll"),

    [switch]$IncludeSpecialNames
)

$gxPath = "C:\Program Files (x86)\GeneXus\GeneXus18"
$results = @()

foreach ($dllName in $Dlls) {
    $dllPath = Join-Path $gxPath $dllName
    if (-not (Test-Path $dllPath)) { continue }

    try {
        $asm = [Reflection.Assembly]::LoadFrom($dllPath)
    } catch {
        Write-Warning "Could not load $dllName"
        continue
    }

    Write-Host "Scanning $dllName..." -ForegroundColor Gray

    foreach ($type in $asm.GetTypes()) {
        # Search in Type Name
        if ($type.FullName -like "*$Query*") {
            $results += [PSCustomObject]@{ Dll = $dllName; Type = $type.FullName; Member = "(Type)"; MatchType = "TypeName" }
        }

        # Search in Members
        $bindingFlags = [Reflection.BindingFlags]::Public -bor [Reflection.BindingFlags]::Instance -bor [Reflection.BindingFlags]::Static -bor [Reflection.BindingFlags]::NonPublic
        $type.GetMembers($bindingFlags) | ForEach-Object {
            if ($_.Name -like "*$Query*") {
                if (-not $IncludeSpecialNames -and ($_.Name.StartsWith("get_") -or $_.Name.StartsWith("set_") -or $_.Name.StartsWith("add_") -or $_.Name.StartsWith("remove_"))) {
                    return
                }
                $results += [PSCustomObject]@{ Dll = $dllName; Type = $type.FullName; Member = $_.Name; MatchType = $_.MemberType.ToString() }
            }
        }
    }
}

if ($results.Count -eq 0) {
    Write-Host "No matches found for '$Query'." -ForegroundColor Red
} else {
    Write-Host "`nFound $($results.Count) matches for '$Query':" -ForegroundColor Green
    $results | Sort-Object Dll, Type | Format-Table -AutoSize
}
