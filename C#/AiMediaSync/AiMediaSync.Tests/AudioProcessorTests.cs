// AudioProcessorTests.cs
using AiMediaSync.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace AiMediaSync.Tests;

public class AudioProcessorTests
{
    private readonly Mock<ILogger<AudioProcessor>> _loggerMock;
    private readonly AudioProcessor _audioProcessor;

    public AudioProcessorTests()
    {
        _loggerMock = new Mock<ILogger<AudioProcessor>>();
        _audioProcessor = new AudioProcessor(_loggerMock.Object);
    }

    [Fact]
    public async Task ExtractFeaturesAsync_WithValidAudio_ReturnsAudioFeatures()
    {
        // Arrange
        var audioData = GenerateTestAudioData(16000); // 1 second of test data
        const int sampleRate = 16000;

        // Act
        var result = await _audioProcessor.ExtractFeaturesAsync(audioData, sampleRate);

        // Assert
        result.Should().NotBeNull();
        result.SampleRate.Should().Be(sampleRate);
        result.AudioLength.Should().BeApproximately(1.0f, 0.1f);
        result.MFCC.Should().NotBeNull();
        result.MelSpectrogram.Should().NotBeNull();
        result.SpectralCentroid.Should().NotBeNull();
        result.ChromaFeatures.Should().NotBeNull();
        result.SpectralRolloff.Should().NotBeNull();
        result.ZeroCrossingRate.Should().NotBeNull();
    }

    [Fact]
    public async Task ResampleAudioAsync_WithDifferentSampleRates_ReturnsCorrectLength()
    {
        // Arrange
        var originalSampleRate = 44100;
        var targetSampleRate = 16000;
        var audioData = GenerateTestAudioData(originalSampleRate);

        // Act
        var result = await _audioProcessor.ResampleAudioAsync(audioData, originalSampleRate, targetSampleRate);

        // Assert
        var expectedLength = (int)(audioData.Length * (double)targetSampleRate / originalSampleRate);
        result.Length.Should().BeCloseTo(expectedLength, 100);
    }

    [Fact]
    public async Task AlignAudioWithVideoAsync_WithValidInputs_ReturnsAlignedFeatures()
    {
        // Arrange
        var audioData = GenerateTestAudioData(16000);
        var audioFeatures = await _audioProcessor.ExtractFeaturesAsync(audioData, 16000);
        const int videoFrameCount = 25; // 1 second at 25 FPS
        const double videoFps = 25.0;

        // Act
        var result = await _audioProcessor.AlignAudioWithVideoAsync(audioFeatures, videoFrameCount, videoFps);

        // Assert
        result.Should().NotBeNull();
        result.GetLength(0).Should().Be(videoFrameCount);
        result.GetLength(1).Should().Be(audioFeatures.MelSpectrogram.GetLength(0));
    }

    private float[] GenerateTestAudioData(int sampleRate, double durationSeconds = 1.0)
    {
        var sampleCount = (int)(sampleRate * durationSeconds);
        var audioData = new float[sampleCount];
        
        // Generate a simple sine wave
        for (int i = 0; i < sampleCount; i++)
        {
            audioData[i] = (float)Math.Sin(2 * Math.PI * 440 * i / sampleRate) * 0.5f;
        }
        
        return audioData;
    }
}

// GuidanceSystemTests.cs
using AiMediaSync.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace AiMediaSync.Tests;

public class GuidanceSystemTests
{
    private readonly Mock<ILogger<GuidanceSystem>> _loggerMock;
    private readonly GuidanceSystem _guidanceSystem;

    public GuidanceSystemTests()
    {
        _loggerMock = new Mock<ILogger<GuidanceSystem>>();
        _guidanceSystem = new GuidanceSystem(_loggerMock.Object);
    }

    [Theory]
    [InlineData(0.5f, 10, 100, 0.7f)]
    [InlineData(0.1f, 50, 100, 0.7f)]
    [InlineData(0.9f, 0, 100, 0.7f)]
    public void ComputeGuidanceStrength_WithVariousInputs_ReturnsValidRange(
        float audioPower, int frameIndex, int totalFrames, float baseStrength)
    {
        // Act
        var result = _guidanceSystem.ComputeGuidanceStrength(audioPower, frameIndex, totalFrames, baseStrength);

        // Assert
        result.Should().BeGreaterOrEqualTo(0.1f);
        result.Should().BeLessOrEqualTo(1.0f);
    }

    [Fact]
    public void ComputeTemporalConsistency_WithSimilarFeatures_ReturnsHighScore()
    {
        // Arrange
        var currentFeatures = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var previousFeatures = new float[] { 1.1f, 2.1f, 2.9f, 4.1f };

        // Act
        var result = _guidanceSystem.ComputeTemporalConsistency(currentFeatures, previousFeatures);

        // Assert
        result.Should().BeGreaterThan(0.8f);
    }

    [Fact]
    public void AdaptiveGuidanceScaling_WithValidInputs_ReturnsScalingFactor()
    {
        // Arrange
        const float audioEnergy = 0.5f;
        const float visualComplexity = 0.3f;

        // Act
        var result = _guidanceSystem.AdaptiveGuidanceScaling(audioEnergy, visualComplexity);

        // Assert
        result.Should().BeGreaterOrEqualTo(0.3f);
        result.Should().BeLessOrEqualTo(1.5f);
    }
}

// IntegrationTests.cs
using AiMediaSync.Core.Extensions;
using AiMediaSync.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using FluentAssertions;

namespace AiMediaSync.Tests;

public class IntegrationTests
{
    [Fact]
    public void ServiceCollection_AddAiMediaSyncServices_RegistersAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAiMediaSyncServices();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<AiMediaSyncService>().Should().NotBeNull();
        serviceProvider.GetService<IAudioProcessor>().Should().NotBeNull();
        serviceProvider.GetService<IFaceProcessor>().Should().NotBeNull();
        serviceProvider.GetService<IVideoProcessor>().Should().NotBeNull();
        serviceProvider.GetService<ILipSyncModel>().Should().NotBeNull();
        serviceProvider.GetService<IGuidanceSystem>().Should().NotBeNull();
    }

    [Fact]
    public async Task AiMediaSyncService_ValidateInputs_WithValidFiles_ReturnsTrue()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAiMediaSyncServices();
        var serviceProvider = services.BuildServiceProvider();
        var syncService = serviceProvider.GetRequiredService<AiMediaSyncService>();

        // Create temporary test files
        var tempDir = Path.GetTempPath();
        var videoPath = Path.Combine(tempDir, "test.mp4");
        var audioPath = Path.Combine(tempDir, "test.wav");
        var outputPath = Path.Combine(tempDir, "output.mp4");

        await File.WriteAllBytesAsync(videoPath, new byte[] { 0x00, 0x00, 0x00, 0x18 }); // Minimal MP4 header
        await File.WriteAllBytesAsync(audioPath, new byte[] { 0x52, 0x49, 0x46, 0x46 }); // Minimal WAV header

        try
        {
            // Act
            var result = syncService.ValidateInputs(videoPath, audioPath, outputPath);

            // Assert
            result.Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (File.Exists(videoPath)) File.Delete(videoPath);
            if (File.Exists(audioPath)) File.Delete(audioPath);
        }
    }

    [Fact]
    public async Task AiMediaSyncService_GetSystemStatistics_ReturnsValidData()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAiMediaSyncServices();
        var serviceProvider = services.BuildServiceProvider();
        var syncService = serviceProvider.GetRequiredService<AiMediaSyncService>();

        // Act
        var stats = await syncService.GetSystemStatisticsAsync();

        // Assert
        stats.Should().NotBeNull();
        stats.Should().ContainKey("MachineName");
        stats.Should().ContainKey("ProcessorCount");
        stats.Should().ContainKey("AudioProcessorReady");
        stats.Should().ContainKey("ModelLoaded");
    }
}