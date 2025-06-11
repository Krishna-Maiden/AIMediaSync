# Scripts/setup-dev-env.ps1
#!/usr/bin/env pwsh

# AiMediaSync Development Environment Setup Script

Write-Host "🚀 Setting up AiMediaSync Development Environment..." -ForegroundColor Green

# Check prerequisites
Write-Host "📋 Checking prerequisites..." -ForegroundColor Yellow

# Check .NET SDK
$dotnetVersion = dotnet --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ .NET SDK not found. Please install .NET 8.0 SDK from https://dotnet.microsoft.com/download"
    exit 1
}
Write-Host "✅ .NET SDK $dotnetVersion found" -ForegroundColor Green

# Check Git
$gitVersion = git --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Warning "⚠️ Git not found. Please install Git from https://git-scm.com/"
} else {
    Write-Host "✅ Git found" -ForegroundColor Green
}

# Create necessary directories
Write-Host "📁 Creating project directories..." -ForegroundColor Yellow
$directories = @("Models", "TestData", "Temp", "Output", "Logs")
foreach ($dir in $directories) {
    if (!(Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "✅ Created directory: $dir" -ForegroundColor Green
    }
}

# Create .gitkeep files for empty directories
@("Models", "TestData", "Temp", "Output") | ForEach-Object {
    $keepFile = Join-Path $_ ".gitkeep"
    if (!(Test-Path $keepFile)) {
        New-Item -ItemType File -Path $keepFile -Force | Out-Null
    }
}

# Restore NuGet packages
Write-Host "📦 Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Failed to restore NuGet packages"
    exit 1
}
Write-Host "✅ NuGet packages restored successfully" -ForegroundColor Green

# Build solution
Write-Host "🔨 Building solution..." -ForegroundColor Yellow
dotnet build --configuration Debug --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Build failed"
    exit 1
}
Write-Host "✅ Solution built successfully" -ForegroundColor Green

# Run tests
Write-Host "🧪 Running tests..." -ForegroundColor Yellow
dotnet test --no-build --verbosity normal
if ($LASTEXITCODE -ne 0) {
    Write-Warning "⚠️ Some tests failed. Please review test results."
} else {
    Write-Host "✅ All tests passed" -ForegroundColor Green
}

# Download models (optional)
Write-Host "📥 Downloading AI models..." -ForegroundColor Yellow
& "$PSScriptRoot/download-models.ps1"

Write-Host ""
Write-Host "🎉 Development environment setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Download AI models if not already done: ./Scripts/download-models.ps1"
Write-Host "2. Add test video and audio files to TestData/ directory"
Write-Host "3. Run the console application: dotnet run --project AiMediaSync.Console"
Write-Host "4. Or start the API: dotnet run --project AiMediaSync.API"

# Scripts/download-models.ps1
#!/usr/bin/env pwsh

# AiMediaSync Model Download Script

Write-Host "📥 Downloading AiMediaSync AI Models..." -ForegroundColor Green

$ModelsDir = "Models"
if (!(Test-Path $ModelsDir)) {
    New-Item -ItemType Directory -Path $ModelsDir -Force | Out-Null
}

# OpenCV Face Detection Models
Write-Host "⬇️ Downloading OpenCV face detection models..." -ForegroundColor Yellow

$faceDetectorConfig = "https://raw.githubusercontent.com/opencv/opencv_extra/master/testdata/dnn/opencv_face_detector.pbtxt"
$faceDetectorModel = "https://github.com/opencv/opencv_3rdparty/raw/dnn_samples_face_detector_20170830/opencv_face_detector_uint8.pb"

try {
    Invoke-WebRequest -Uri $faceDetectorConfig -OutFile "$ModelsDir/opencv_face_detector.pbtxt"
    Write-Host "✅ Downloaded opencv_face_detector.pbtxt" -ForegroundColor Green
    
    Invoke-WebRequest -Uri $faceDetectorModel -OutFile "$ModelsDir/opencv_face_detector_uint8.pb"
    Write-Host "✅ Downloaded opencv_face_detector_uint8.pb" -ForegroundColor Green
} catch {
    Write-Warning "⚠️ Failed to download OpenCV models: $_"
}

# Create placeholder for other models
Write-Host "📝 Creating model placeholders..." -ForegroundColor Yellow

$placeholderModels = @(
    @{
        Name = "facial_landmarks.onnx"
        Description = "Facial landmark detection model (68 points)"
        Source = "https://github.com/tensorflow/tensorflow/tree/master/tensorflow/lite/models/face_landmark"
        Note = "Download or train a 68-point facial landmark model and place here"
    },
    @{
        Name = "lipsync_model.onnx"
        Description = "Lip-sync generation model"
        Source = "Custom trained or Wav2Lip converted to ONNX"
        Note = "Train custom model or convert existing PyTorch model to ONNX format"
    }
)

foreach ($model in $placeholderModels) {
    $readmePath = "$ModelsDir/$($model.Name).README.txt"
    $content = @"
Model: $($model.Name)
Description: $($model.Description)
Source: $($model.Source)
Note: $($model.Note)

To use this model:
1. Obtain or train the model
2. Convert to ONNX format if necessary
3. Place the .onnx file in this directory
4. Update configuration in appsettings.json if needed
"@
    Set-Content -Path $readmePath -Value $content
    Write-Host "📄 Created README for $($model.Name)" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "✅ Model download script completed!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Model Status:" -ForegroundColor Cyan
Write-Host "✅ OpenCV Face Detection - Ready to use"
Write-Host "⏳ Facial Landmarks - Need to download/train"
Write-Host "⏳ Lip-Sync Model - Need to download/train"
Write-Host ""
Write-Host "💡 For production use, you'll need to:" -ForegroundColor Yellow
Write-Host "1. Train or obtain facial landmark detection model"
Write-Host "2. Train or convert lip-sync model (Wav2Lip, SadTalker, etc.)"
Write-Host "3. Place .onnx files in Models/ directory"

# Scripts/build.ps1
#!/usr/bin/env pwsh

# AiMediaSync Build Script

param(
    [Parameter()]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [Parameter()]
    [switch]$Clean,
    
    [Parameter()]
    [switch]$Test,
    
    [Parameter()]
    [switch]$Publish
)

Write-Host "🔨 Building AiMediaSync Solution..." -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan

# Clean if requested
if ($Clean) {
    Write-Host "🧹 Cleaning solution..." -ForegroundColor Yellow
    dotnet clean --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ Clean failed"
        exit 1
    }
    Write-Host "✅ Clean completed" -ForegroundColor Green
}

# Restore packages
Write-Host "📦 Restoring packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Package restore failed"
    exit 1
}
Write-Host "✅ Packages restored" -ForegroundColor Green

# Build solution
Write-Host "🔨 Building solution..." -ForegroundColor Yellow
dotnet build --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Build failed"
    exit 1
}
Write-Host "✅ Build completed successfully" -ForegroundColor Green

# Run tests if requested
if ($Test) {
    Write-Host "🧪 Running tests..." -ForegroundColor Yellow
    dotnet test --configuration $Configuration --no-build --logger "console;verbosity=normal"
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "⚠️ Some tests failed"
    } else {
        Write-Host "✅ All tests passed" -ForegroundColor Green
    }
}

# Publish if requested
if ($Publish) {
    Write-Host "📦 Publishing applications..." -ForegroundColor Yellow
    
    # Publish Console App
    dotnet publish AiMediaSync.Console --configuration $Configuration --output "publish/console" --no-build
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Console app published to publish/console" -ForegroundColor Green
    }
    
    # Publish API
    dotnet publish AiMediaSync.API --configuration $Configuration --output "publish/api" --no-build
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ API published to publish/api" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "🎉 Build script completed!" -ForegroundColor Green