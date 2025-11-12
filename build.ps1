# Build script for Jellyfin Ratings Plugin
# PowerShell script for Windows

param(
    [string]$Configuration = "Release"
)

Write-Host "Building Jellyfin Ratings Plugin..." -ForegroundColor Green

# Clean previous builds
if (Test-Path "bin") {
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force "bin"
}

if (Test-Path "obj") {
    Remove-Item -Recurse -Force "obj"
}

# Build the project
Write-Host "Building project with configuration: $Configuration" -ForegroundColor Yellow
dotnet build --configuration $Configuration

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nBuild successful!" -ForegroundColor Green
    Write-Host "Output: bin\$Configuration\net8.0\Jellyfin.Plugin.Ratings.dll" -ForegroundColor Cyan

    Write-Host "`nTo install the plugin:" -ForegroundColor Yellow
    Write-Host "1. Copy the DLL to your Jellyfin plugins folder:" -ForegroundColor White
    Write-Host "   Windows: %AppData%\Jellyfin\Server\plugins\Ratings_1.0.0.0\" -ForegroundColor Gray
    Write-Host "   Linux: /var/lib/jellyfin/plugins/Ratings_1.0.0.0/" -ForegroundColor Gray
    Write-Host "2. Restart Jellyfin" -ForegroundColor White
} else {
    Write-Host "`nBuild failed!" -ForegroundColor Red
    exit 1
}
