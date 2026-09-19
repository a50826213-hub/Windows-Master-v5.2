$ErrorActionPreference = 'Stop'

Write-Host '=== Windows Master 5.2 Native Build ===' -ForegroundColor Cyan

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet was not found. Install .NET 10 SDK and Visual Studio 2026 with WinUI application development.'
}

Write-Host 'Restoring packages...' -ForegroundColor Yellow
dotnet restore

Write-Host 'Building x64 Release...' -ForegroundColor Yellow
dotnet build -c Release -p:Platform=x64

Write-Host 'Publishing self-contained x64...' -ForegroundColor Yellow
dotnet publish -c Release -r win-x64 --self-contained true

Write-Host 'Searching for publish output...' -ForegroundColor Yellow
Get-ChildItem -Path . -Recurse -Filter 'WindowsMaster.exe' | ForEach-Object {
    Write-Host "EXE: $($_.FullName)" -ForegroundColor Green
}

Write-Host 'Done.' -ForegroundColor Green
