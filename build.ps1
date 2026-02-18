# GeneXus MCP - Build & Deploy Script
# ==========================================

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$publishDir = Join-Path $root "publish"

Write-Host "🚧 Building Solutions..." -ForegroundColor Cyan

# 1. Build Gateway (.NET 8)
Write-Host "   > Building Gateway..."
dotnet publish "src\GxMcp.Gateway\GxMcp.Gateway.csproj" -c Release -o "$publishDir" --nologo

# 2. Build Worker (.NET Framework 4.8)
Write-Host "   > Building Worker..."
# We use msbuild or dotnet build for the worker. 'dotnet build' works if .NET SDK supports 4.8 targeting.
dotnet build "src\GxMcp.Worker\GxMcp.Worker.csproj" -c Release --nologo

# 3. Copy Worker Binaries to Publish
$workerBin = Join-Path "src" "GxMcp.Worker"
$workerBin = Join-Path $workerBin "bin"
$workerBin = Join-Path $workerBin "Release"
Write-Host "   > Deploying Worker binaries from $workerBin..."
Copy-Item "$workerBin\*" -Destination "$publishDir" -Recurse -Force

# 4. Copy Config Template if missing
if (-not (Test-Path "$publishDir\config.json")) {
    Write-Host "   > Creating default config.json..."
    $defaultConfig = @{
        GeneXus = @{
            InstallationPath = "C:\\Program Files (x86)\\GeneXus\\GeneXus18"
            WorkerExecutable = "$publishDir\\GxMcp.Worker.exe"
        }
        Server = @{
            HttpPort = 5000
            McpStdio = $true
        }
        Logging = @{
            Level = "Debug"
            Path = "logs"
        }
        Environment = @{
            KBPath = "C:\\KBs\\YourKB"
        }
    } | ConvertTo-Json -Depth 4
    Set-Content "$publishDir\config.json" $defaultConfig
}

Write-Host "✅ Build Complete!" -ForegroundColor Green
Write-Host "   - Output: $publishDir"
Write-Host "   - Worker: $publishDir\GxMcp.Worker.exe"
Write-Host "   - Gateway: $publishDir\GxMcp.Gateway.exe"
