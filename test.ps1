#!/usr/bin/env pwsh

param(
    [string]$Filter = "",
    [switch]$Coverage,
    [switch]$Watch
)

Write-Host "🧪 Running GitGen Tests" -ForegroundColor Magenta
Write-Host ""

$testProject = "tests/GitGen.Tests/GitGen.Tests.csproj"

# Build the test project first
Write-Host "📦 Building test project..." -ForegroundColor Cyan
dotnet build $testProject -c Release --nologo --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

# Prepare test command
$testArgs = @("test", $testProject, "-c", "Release", "--no-build", "--nologo")

if ($Filter) {
    Write-Host "🔍 Filter: $Filter" -ForegroundColor Yellow
    $testArgs += "--filter"
    $testArgs += $Filter
}

if ($Coverage) {
    Write-Host "📊 Running with code coverage..." -ForegroundColor Yellow
    $testArgs += "--collect:`"XPlat Code Coverage`""
    $testArgs += "--results-directory"
    $testArgs += "TestResults"
}

if ($Watch) {
    Write-Host "👁️ Running in watch mode..." -ForegroundColor Yellow
    $watchArgs = @("watch", "--project", $testProject) + $testArgs
    & dotnet $watchArgs
} else {
    # Run tests
    & dotnet $testArgs

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "✅ All tests passed!" -ForegroundColor Green
        
        if ($Coverage) {
            Write-Host ""
            Write-Host "📊 Coverage report generated in TestResults folder" -ForegroundColor Cyan
            Write-Host "   Install ReportGenerator to view HTML reports:" -ForegroundColor Gray
            Write-Host "   dotnet tool install -g dotnet-reportgenerator-globaltool" -ForegroundColor Gray
        }
    } else {
        Write-Host ""
        Write-Host "❌ Tests failed!" -ForegroundColor Red
        exit 1
    }
}