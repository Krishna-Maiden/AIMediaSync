# Scripts/test.ps1
#!/usr/bin/env pwsh

# AiMediaSync Test Script

param(
    [Parameter()]
    [string]$Filter = "",
    
    [Parameter()]
    [switch]$Coverage,
    
    [Parameter()]
    [switch]$Watch,
    
    [Parameter()]
    [switch]$Integration,
    
    [Parameter()]
    [switch]$Performance,
    
    [Parameter()]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

Write-Host "🧪 Running AiMediaSync Tests..." -ForegroundColor Green

$testArgs = @(
    "test"
    "--configuration", $Configuration
    "--logger", "console;verbosity=normal"
    "--logger", "trx"
    "--results-directory", "TestResults"
    "--no-build"
)

if ($Filter) {
    $testArgs += "--filter", $Filter
    Write-Host "🔍 Filter: $Filter" -ForegroundColor Cyan
}

if ($Integration) {
    $testArgs += "--filter", "Category=Integration"
    Write-Host "🔗 Running integration tests" -ForegroundColor Cyan
}

if ($Performance) {
    $testArgs += "--filter", "Category=Performance"
    Write-Host "⚡ Running performance tests" -ForegroundColor Cyan
}

if ($Coverage) {
    $testArgs += "--collect", "XPlat Code Coverage"
    $testArgs += "--settings", "coverlet.runsettings"
    Write-Host "📊 Code coverage enabled" -ForegroundColor Cyan
}

if ($Watch) {
    $testArgs += "--watch"
    Write-Host "👀 Watch mode enabled" -ForegroundColor Cyan
}

# Create TestResults directory
if (!(Test-Path "TestResults")) {
    New-Item -ItemType Directory -Path "TestResults" -Force | Out-Null
}

# Create coverage settings file
$coverletSettings = @"
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat code coverage">
        <Configuration>
          <Format>opencover,cobertura,json,lcov</Format>
          <Exclude>[*]*.Program,[*]*.Startup,[*]*Migrations.*</Exclude>
          <ExcludeByFile>**/Migrations/**/*.cs</ExcludeByFile>
          <IncludeTestAssembly>false</IncludeTestAssembly>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
"@

Set-Content -Path "coverlet.runsettings" -Value $coverletSettings

Write-Host "📋 Test Configuration:" -ForegroundColor Yellow
Write-Host "  Configuration: $Configuration"
Write-Host "  Coverage: $Coverage"
Write-Host "  Watch: $Watch"
Write-Host "  Filter: $(if ($Filter) { $Filter } else { 'All tests' })"
Write-Host ""

# Build first
Write-Host "🔨 Building solution..." -ForegroundColor Yellow
dotnet build --configuration $Configuration --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Build failed"
    exit 1
}

# Run tests
Write-Host "🚀 Executing tests..." -ForegroundColor Yellow
$startTime = Get-Date
& dotnet @testArgs

$endTime = Get-Date
$duration = $endTime - $startTime

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ All tests passed!" -ForegroundColor Green
    Write-Host "⏱️ Duration: $($duration.TotalSeconds.ToString('F2')) seconds" -ForegroundColor Cyan
} else {
    Write-Host ""
    Write-Host "❌ Some tests failed" -ForegroundColor Red
    Write-Host "⏱️ Duration: $($duration.TotalSeconds.ToString('F2')) seconds" -ForegroundColor Cyan
}

# Process coverage if enabled
if ($Coverage -and $LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "📊 Processing code coverage..." -ForegroundColor Yellow
    
    # Find coverage files
    $coverageFiles = Get-ChildItem -Path "TestResults" -Recurse -Filter "coverage.cobertura.xml"
    
    if ($coverageFiles.Count -gt 0) {
        $latestCoverage = $coverageFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        Write-Host "✅ Coverage report generated: $($latestCoverage.FullName)" -ForegroundColor Green
        
        # Install and run reportgenerator if available
        try {
            # Check if reportgenerator is installed
            $reportGenInstalled = Get-Command reportgenerator -ErrorAction SilentlyContinue
            if (-not $reportGenInstalled) {
                Write-Host "📥 Installing ReportGenerator..." -ForegroundColor Yellow
                dotnet tool install --global dotnet-reportgenerator-globaltool --verbosity quiet
            }
            
            # Generate HTML report
            Write-Host "📈 Generating HTML coverage report..." -ForegroundColor Yellow
            reportgenerator "-reports:$($latestCoverage.FullName)" "-targetdir:TestResults/CoverageReport" "-reporttypes:Html;Badges"
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ HTML coverage report: TestResults/CoverageReport/index.html" -ForegroundColor Green
                
                # Show coverage summary
                $htmlReport = "TestResults/CoverageReport/index.html"
                if (Test-Path $htmlReport) {
                    Write-Host "🌐 Open coverage report: file://$(Resolve-Path $htmlReport)" -ForegroundColor Cyan
                }
            }
        } catch {
            Write-Host "⚠️ Failed to generate HTML report: $_" -ForegroundColor Yellow
            Write-Host "💡 Install reportgenerator: dotnet tool install --global dotnet-reportgenerator-globaltool" -ForegroundColor Yellow
        }
        
        # Parse and display coverage summary
        try {
            [xml]$coverageXml = Get-Content $latestCoverage.FullName
            $summary = $coverageXml.coverage
            $lineRate = [math]::Round([double]$summary.'line-rate' * 100, 2)
            $branchRate = [math]::Round([double]$summary.'branch-rate' * 100, 2)
            
            Write-Host ""
            Write-Host "📊 Coverage Summary:" -ForegroundColor Cyan
            Write-Host "  Line Coverage: $lineRate%" -ForegroundColor $(if ($lineRate -ge 80) { 'Green' } elseif ($lineRate -ge 60) { 'Yellow' } else { 'Red' })
            Write-Host "  Branch Coverage: $branchRate%" -ForegroundColor $(if ($branchRate -ge 70) { 'Green' } elseif ($branchRate -ge 50) { 'Yellow' } else { 'Red' })
        } catch {
            Write-Host "⚠️ Could not parse coverage summary" -ForegroundColor Yellow
        }
    } else {
        Write-Host "⚠️ No coverage files found" -ForegroundColor Yellow
    }
}

# Show test result summary
Write-Host ""
Write-Host "📋 Test Categories Available:" -ForegroundColor Cyan
Write-Host "  Unit Tests:        dotnet test --filter Category=Unit"
Write-Host "  Integration Tests: dotnet test --filter Category=Integration"
Write-Host "  Performance Tests: dotnet test --filter Category=Performance"
Write-Host "  Smoke Tests:       dotnet test --filter Category=Smoke"
Write-Host ""
Write-Host "📊 Coverage Options:" -ForegroundColor Cyan
Write-Host "  With Coverage:     ./Scripts/test.ps1 -Coverage"
Write-Host "  Watch Mode:        ./Scripts/test.ps1 -Watch"
Write-Host "  Specific Filter:   ./Scripts/test.ps1 -Filter 'TestName'"

# Cleanup
if (Test-Path "coverlet.runsettings") {
    Remove-Item "coverlet.runsettings" -Force
}

Write-Host ""
Write-Host "🎉 Test script completed!" -ForegroundColor Green

exit $LASTEXITCODE

# Scripts/benchmark.ps1
#!/usr/bin/env pwsh

# AiMediaSync Performance Benchmark Script

param(
    [Parameter()]
    [ValidateSet("Quick", "Standard", "Comprehensive")]
    [string]$Mode = "Standard",
    
    [Parameter()]
    [switch]$GenerateReport,
    
    [Parameter()]
    [string]$OutputPath = "BenchmarkResults"
)

Write-Host "⚡ Running AiMediaSync Performance Benchmarks..." -ForegroundColor Green

# Ensure BenchmarkDotNet is available
Write-Host "📦 Checking BenchmarkDotNet..." -ForegroundColor Yellow
$benchmarkProject = "AiMediaSync.Benchmarks"

if (!(Test-Path "$benchmarkProject/$benchmarkProject.csproj")) {
    Write-Host "📁 Creating benchmark project..." -ForegroundColor Yellow
    
    dotnet new console -n $benchmarkProject
    Set-Location $benchmarkProject
    
    # Add BenchmarkDotNet package
    dotnet add package BenchmarkDotNet
    dotnet add reference "../AiMediaSync.Core/AiMediaSync.Core.csproj"
    
    # Create sample benchmark
    $benchmarkCode = @"
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using AiMediaSync.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiMediaSync.Benchmarks;

public class Program
{
    public static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}

[MemoryDiagnoser]
[SimpleJob]
public class AudioProcessingBenchmarks
{
    private AudioProcessor _audioProcessor;
    private float[] _testAudio;

    [GlobalSetup]
    public void Setup()
    {
        _audioProcessor = new AudioProcessor(NullLogger<AudioProcessor>.Instance);
        _testAudio = GenerateTestAudio(16000); // 1 second at 16kHz
    }

    [Benchmark]
    public async Task ExtractMFCC()
    {
        await _audioProcessor.ExtractFeaturesAsync(_testAudio, 16000);
    }

    [Benchmark]
    public async Task ResampleAudio()
    {
        await _audioProcessor.ResampleAudioAsync(_testAudio, 16000, 8000);
    }

    private float[] GenerateTestAudio(int sampleCount)
    {
        var audio = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            audio[i] = (float)Math.Sin(2 * Math.PI * 440 * i / 16000);
        }
        return audio;
    }
}
"@

    Set-Content -Path "Program.cs" -Value $benchmarkCode
    Set-Location ".."
}

# Run benchmarks
Write-Host "🚀 Executing benchmarks..." -ForegroundColor Yellow
Write-Host "  Mode: $Mode" -ForegroundColor Cyan

$benchmarkArgs = @()

switch ($Mode) {
    "Quick" {
        $benchmarkArgs += "--job", "dry"
        Write-Host "  Quick mode: Dry run for validation" -ForegroundColor Cyan
    }
    "Standard" {
        $benchmarkArgs += "--job", "short"
        Write-Host "  Standard mode: Short run with basic statistics" -ForegroundColor Cyan
    }
    "Comprehensive" {
        $benchmarkArgs += "--job", "long"
        Write-Host "  Comprehensive mode: Full statistical analysis" -ForegroundColor Cyan
    }
}

if ($GenerateReport) {
    $benchmarkArgs += "--exporters", "html,json,csv"
    Write-Host "  Report generation: Enabled" -ForegroundColor Cyan
}

# Ensure output directory exists
if (!(Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

$benchmarkArgs += "--artifacts", $OutputPath

# Build and run benchmarks
Write-Host ""
Write-Host "🔨 Building benchmark project..." -ForegroundColor Yellow
dotnet build $benchmarkProject --configuration Release --verbosity quiet

if ($LASTEXITCODE -eq 0) {
    Write-Host "⚡ Running benchmarks..." -ForegroundColor Yellow
    dotnet run --project $benchmarkProject --configuration Release -- @benchmarkArgs
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "✅ Benchmarks completed successfully!" -ForegroundColor Green
        
        if ($GenerateReport) {
            $reportFiles = Get-ChildItem -Path $OutputPath -Filter "*.html"
            if ($reportFiles.Count -gt 0) {
                $latestReport = $reportFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
                Write-Host "📊 Benchmark report: $($latestReport.FullName)" -ForegroundColor Cyan
            }
        }
    } else {
        Write-Host "❌ Benchmark execution failed" -ForegroundColor Red
    }
} else {
    Write-Host "❌ Benchmark build failed" -ForegroundColor Red
}

Write-Host ""
Write-Host "📈 Performance Tips:" -ForegroundColor Yellow
Write-Host "  • Use Release configuration for accurate results"
Write-Host "  • Close other applications during benchmarking"
Write-Host "  • Run multiple times to ensure consistency"
Write-Host "  • Monitor CPU and memory usage"

Write-Host ""
Write-Host "🎯 Benchmark Complete!" -ForegroundColor Green

# Scripts/deploy.ps1
#!/usr/bin/env pwsh

# AiMediaSync Deployment Script

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Development", "Staging", "Production")]
    [string]$Environment,
    
    [Parameter()]
    [ValidateSet("API", "Console", "All")]
    [string]$Target = "All",
    
    [Parameter()]
    [string]$Version = "1.0.0",
    
    [Parameter()]
    [switch]$Docker,
    
    [Parameter()]
    [switch]$DryRun
)

Write-Host "🚀 AiMediaSync Deployment Script" -ForegroundColor Green
Write-Host "Environment: $Environment" -ForegroundColor Cyan
Write-Host "Target: $Target" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host "🔍 DRY RUN MODE - No actual deployment will occur" -ForegroundColor Yellow
}

# Configuration based on environment
$config = @{
    Development = @{
        BuildConfiguration = "Debug"
        DatabaseName = "AiMediaSyncDb_Dev"
        LogLevel = "Debug"
        EnableSwagger = $true
    }
    Staging = @{
        BuildConfiguration = "Release"
        DatabaseName = "AiMediaSyncDb_Staging"
        LogLevel = "Information"
        EnableSwagger = $true
    }
    Production = @{
        BuildConfiguration = "Release"
        DatabaseName = "AiMediaSyncDb_Prod"
        LogLevel = "Warning"
        EnableSwagger = $false
    }
}

$envConfig = $config[$Environment]

Write-Host ""
Write-Host "📋 Deployment Configuration:" -ForegroundColor Yellow
Write-Host "  Build: $($envConfig.BuildConfiguration)"
Write-Host "  Database: $($envConfig.DatabaseName)"
Write-Host "  Log Level: $($envConfig.LogLevel)"
Write-Host "  Swagger: $($envConfig.EnableSwagger)"

# Pre-deployment checks
Write-Host ""
Write-Host "🔍 Pre-deployment checks..." -ForegroundColor Yellow

# Check if solution builds
Write-Host "  Checking build..." -ForegroundColor Gray
dotnet build --configuration $envConfig.BuildConfiguration --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Build failed"
    exit 1
}
Write-Host "  ✅ Build successful" -ForegroundColor Green

# Run tests
Write-Host "  Running tests..." -ForegroundColor Gray
dotnet test --configuration $envConfig.BuildConfiguration --verbosity quiet --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Warning "⚠️ Some tests failed - continuing deployment"
} else {
    Write-Host "  ✅ Tests passed" -ForegroundColor Green
}

# Check required files
$requiredFiles = @(
    "Models/.gitkeep",
    "TestData/.gitkeep"
)

foreach ($file in $requiredFiles) {
    if (Test-Path $file) {
        Write-Host "  ✅ $file exists" -ForegroundColor Green
    } else {
        Write-Host "  📁 Creating $file" -ForegroundColor Yellow
        $dir = Split-Path $file -Parent
        if (!(Test-Path $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
        }
        New-Item -ItemType File -Path $file -Force | Out-Null
    }
}

if ($DryRun) {
    Write-Host ""
    Write-Host "🔍 DRY RUN - Deployment steps that would be executed:" -ForegroundColor Yellow
    Write-Host "  1. Build applications in $($envConfig.BuildConfiguration) mode"
    Write-Host "  2. Publish applications to deploy/$Environment/"
    Write-Host "  3. Copy configuration files"
    Write-Host "  4. Update database (if needed)"
    Write-Host "  5. Deploy to target environment"
    
    if ($Docker) {
        Write-Host "  6. Build Docker images"
        Write-Host "  7. Push to container registry"
    }
    
    Write-Host ""
    Write-Host "✅ Dry run completed" -ForegroundColor Green
    exit 0
}

# Create deployment directory
$deployPath = "deploy/$Environment"
if (Test-Path $deployPath) {
    Remove-Item $deployPath -Recurse -Force
}
New-Item -ItemType Directory -Path $deployPath -Force | Out-Null

# Deploy API if requested
if ($Target -eq "API" -or $Target -eq "All") {
    Write-Host ""
    Write-Host "🌐 Deploying API..." -ForegroundColor Yellow
    
    $apiDeployPath = "$deployPath/api"
    dotnet publish AiMediaSync.API --configuration $envConfig.BuildConfiguration --output $apiDeployPath --verbosity quiet
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✅ API published to $apiDeployPath" -ForegroundColor Green
        
        # Copy additional files
        Copy-Item "Models" $apiDeployPath -Recurse -Force
        Copy-Item "Scripts" $apiDeployPath -Recurse -Force
        
        # Update configuration for environment
        $configFile = "$apiDeployPath/appsettings.json"
        if (Test-Path $configFile) {
            $config = Get-Content $configFile | ConvertFrom-Json
            $config.Logging.LogLevel.Default = $envConfig.LogLevel
            $config.ConnectionStrings.DefaultConnection = $config.ConnectionStrings.DefaultConnection -replace "AiMediaSyncDb", $envConfig.DatabaseName
            $config | ConvertTo-Json -Depth 10 | Set-Content $configFile
        }
    } else {
        Write-Error "❌ API deployment failed"
        exit 1
    }
}

# Deploy Console if requested
if ($Target -eq "Console" -or $Target -eq "All") {
    Write-Host ""
    Write-Host "💻 Deploying Console..." -ForegroundColor Yellow
    
    $consoleDeployPath = "$deployPath/console"
    dotnet publish AiMediaSync.Console --configuration $envConfig.BuildConfiguration --output $consoleDeployPath --verbosity quiet
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✅ Console published to $consoleDeployPath" -ForegroundColor Green
        
        # Copy additional files
        Copy-Item "Models" $consoleDeployPath -Recurse -Force
        Copy-Item "TestData" $consoleDeployPath -Recurse -Force
    } else {
        Write-Error "❌ Console deployment failed"
        exit 1
    }
}

# Docker deployment
if ($Docker) {
    Write-Host ""
    Write-Host "🐳 Building Docker images..." -ForegroundColor Yellow
    
    # API Docker image
    if ($Target -eq "API" -or $Target -eq "All") {
        $apiImageName = "aimediasync-api:$Version"
        docker build -t $apiImageName -f Dockerfile.api .
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✅ API Docker image built: $apiImageName" -ForegroundColor Green
        } else {
            Write-Error "❌ API Docker build failed"
        }
    }
    
    # Console Docker image  
    if ($Target -eq "Console" -or $Target -eq "All") {
        $consoleImageName = "aimediasync-console:$Version"
        docker build -t $consoleImageName -f Dockerfile.console .
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✅ Console Docker image built: $consoleImageName" -ForegroundColor Green
        } else {
            Write-Error "❌ Console Docker build failed"
        }
    }
}

# Create deployment package
Write-Host ""
Write-Host "📦 Creating deployment package..." -ForegroundColor Yellow

$packageName = "AiMediaSync-$Environment-$Version-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$packagePath = "$packageName.zip"

if (Get-Command Compress-Archive -ErrorAction SilentlyContinue) {
    Compress-Archive -Path $deployPath -DestinationPath $packagePath -Force
    Write-Host "  ✅ Deployment package created: $packagePath" -ForegroundColor Green
} else {
    Write-Host "  ⚠️ Compress-Archive not available, skipping package creation" -ForegroundColor Yellow
}

# Generate deployment notes
$deploymentNotes = @"
# AiMediaSync Deployment Notes

**Environment:** $Environment
**Version:** $Version
**Date:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
**Target:** $Target

## Configuration
- Build: $($envConfig.BuildConfiguration)
- Database: $($envConfig.DatabaseName)
- Log Level: $($envConfig.LogLevel)
- Swagger: $($envConfig.EnableSwagger)

## Deployment Contents
"@

if ($Target -eq "API" -or $Target -eq "All") {
    $deploymentNotes += "`n- API: $deployPath/api"
}

if ($Target -eq "Console" -or $Target -eq "All") {
    $deploymentNotes += "`n- Console: $deployPath/console"
}

$deploymentNotes += @"

## Post-Deployment Steps
1. Update configuration files for $Environment environment
2. Run database migrations if needed
3. Verify model files are in place
4. Test health endpoints
5. Monitor logs for any issues

## Rollback Plan
1. Stop services
2. Deploy previous version
3. Restore database backup if needed
4. Restart services
"@

Set-Content -Path "$deployPath/DEPLOYMENT_NOTES.md" -Value $deploymentNotes

Write-Host ""
Write-Host "✅ Deployment completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "📂 Deployment location: $deployPath" -ForegroundColor Cyan
if (Test-Path $packagePath) {
    Write-Host "📦 Package: $packagePath" -ForegroundColor Cyan
}
Write-Host ""
Write-Host "🔗 Next steps:" -ForegroundColor Yellow
Write-Host "  1. Review deployment notes: $deployPath/DEPLOYMENT_NOTES.md"
Write-Host "  2. Deploy to target environment"
Write-Host "  3. Run health checks"
Write-Host "  4. Monitor application logs"