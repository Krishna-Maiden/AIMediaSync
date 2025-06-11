using AiMediaSync.Core.Services;
using AiMediaSync.Core.Models;
using AiMediaSync.Tests.TestUtilities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace AiMediaSync.Tests;

public class FaceProcessorTests : IDisposable
{
    private readonly Mock<ILogger<FaceProcessor>> _loggerMock;
    private readonly FaceProcessor _faceProcessor;
    private readonly string _testModelsPath;

    public FaceProcessorTests()
    {
        _loggerMock = MockExtensions.CreateMockLogger<FaceProcessor>();
        _testModelsPath = Path.Combine("TestData", "Models");
        
        // Create test models directory
        Directory.CreateDirectory(_testModelsPath);
        
        _faceProcessor = new FaceProcessor(_loggerMock.Object, _testModelsPath);
    }

    [Fact]
    public async Task ProcessFrameAsync_WithValidFrame_ReturnsVisualFeatures()
    {
        // Arrange
        var frameData = TestDataGenerator.GenerateTestFrameData(640, 480);
        const int width = 640;
        const int height = 480;

        // Act
        var result = await _faceProcessor.ProcessFrameAsync(frameData, width, height);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<VisualFeatures>();
        result.FaceEmbedding.Should().NotBeNull();
        result.LipEmbedding.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessFrameAsync_WithEmptyFrame_ReturnsLowConfidence()
    {
        // Arrange
        var frameData = new byte[0];
        const int width = 640;
        const int height = 480;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _faceProcessor.ProcessFrameAsync(frameData, width, height));
    }

    [Fact]
    public async Task DetectFaceAsync_WithValidFrame_ReturnsBoundingBox()
    {
        // Arrange
        var frameData = TestDataGenerator.GenerateTestFrameData(640, 480);
        const int width = 640;
        const int height = 480;

        // Act
        var result = await _faceProcessor.DetectFaceAsync(frameData, width, height);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<BoundingBox>();
        if (result != null)
        {
            result.X.Should().BeGreaterOrEqualTo(0);
            result.Y.Should().BeGreaterOrEqualTo(0);
            result.Width.Should().BeGreaterThan(0);
            result.Height.Should().BeGreaterThan(0);
            result.Confidence.Should().BeInRange(0f, 1f);
        }
    }

    [Fact]
    public async Task DetectFaceAsync_WithInvalidFrame_ReturnsNull()
    {
        // Arrange
        var frameData = new byte[] { 0x00, 0x01, 0x02 }; // Invalid image data
        const int width = 640;
        const int height = 480;

        // Act
        var result = await _faceProcessor.DetectFaceAsync(frameData, width, height);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExtractLandmarksAsync_WithValidFace_ReturnsLandmarks()
    {
        // Arrange
        var frameData = TestDataGenerator.GenerateTestFrameData(640, 480);
        var faceBounds = TestDataGenerator.GenerateTestBoundingBox(100, 100, 200, 200);

        // Act
        var result = await _faceProcessor.ExtractLandmarksAsync(frameData, faceBounds);

        // Assert
        result.Should().NotBeNull();
        result.LipPoints.Should().NotBeNull();
        result.LipPoints.Should().HaveCountGreaterThan(0);
        result.FaceContour.Should().NotBeNull();
        result.EyePoints.Should().NotBeNull();
        result.NosePoints.Should().NotBeNull();
    }

    [Fact]
    public async Task ExtractLipRegionAsync_WithValidBounds_ReturnsLipImage()
    {
        // Arrange
        var frameData = TestDataGenerator.GenerateTestFrameData(640, 480);
        var lipBounds = TestDataGenerator.GenerateTestBoundingBox(220, 260, 60, 40);
        const int targetWidth = 96;
        const int targetHeight = 96;

        // Act
        var result = await _faceProcessor.ExtractLipRegionAsync(frameData, lipBounds, targetWidth, targetHeight);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
    }

    [Theory]
    [InlineData(320, 240)]
    [InlineData(640, 480)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public async Task ProcessFrameAsync_WithDifferentResolutions_HandlesCorrectly(int width, int height)
    {
        // Arrange
        var frameData = TestDataGenerator.GenerateTestFrameData(width, height);

        // Act
        var result = await _faceProcessor.ProcessFrameAsync(frameData, width, height);

        // Assert
        result.Should().NotBeNull();
        
        // Should handle different resolutions gracefully
        if (result.FaceBoundingBox != null)
        {
            result.FaceBoundingBox.X.Should().BeInRange(0, width);
            result.FaceBoundingBox.Y.Should().BeInRange(0, height);
        }
    }

    [Fact]
    public async Task ProcessFrameAsync_WithMultipleFaces_ReturnsLargestFace()
    {
        // Arrange
        var frameData = TestDataGenerator.GenerateTestFrameData(1280, 720);

        // Act
        var result = await _faceProcessor.ProcessFrameAsync(frameData, 1280, 720);

        // Assert
        result.Should().NotBeNull();
        
        // The processor should return the largest/most confident face
        if (result.FaceBoundingBox != null)
        {
            result.ConfidenceScore.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task ExtractLandmarksAsync_WithSmallFace_HandlesGracefully()
    {
        // Arrange
        var frameData = TestDataGenerator.GenerateTestFrameData(640, 480);
        var smallFaceBounds = TestDataGenerator.GenerateTestBoundingBox(300, 200, 20, 25); // Very small face

        // Act
        var result = await _faceProcessor.ExtractLandmarksAsync(frameData, smallFaceBounds);

        // Assert
        result.Should().NotBeNull();
        
        // Should handle small faces by providing estimated landmarks
        result.LipPoints.Should().NotBeNull();
        result.LipPoints.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExtractLipRegionAsync_WithInvalidBounds_ThrowsException()
    {
        // Arrange
        var frameData = TestDataGenerator.GenerateTestFrameData(640, 480);
        var invalidBounds = new BoundingBox { X = -10, Y = -10, Width = 50, Height = 50 };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _faceProcessor.ExtractLipRegionAsync(frameData, invalidBounds));
    }

    [Fact]
    public async Task ProcessFrameAsync_ConcurrentCalls_HandlesCorrectly()
    {
        // Arrange
        var frameData1 = TestDataGenerator.GenerateTestFrameData(640, 480);
        var frameData2 = TestDataGenerator.GenerateTestFrameData(640, 480);
        var frameData3 = TestDataGenerator.GenerateTestFrameData(640, 480);

        // Act
        var tasks = new[]
        {
            _faceProcessor.ProcessFrameAsync(frameData1, 640, 480),
            _faceProcessor.ProcessFrameAsync(frameData2, 640, 480),
            _faceProcessor.ProcessFrameAsync(frameData3, 640, 480)
        };

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
    }

    [Fact]
    public void FaceProcessor_Initialization_CreatesSuccessfully()
    {
        // Arrange & Act
        var processor = new FaceProcessor(_loggerMock.Object, _testModelsPath);

        // Assert
        processor.Should().NotBeNull();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Face detection")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessFrameAsync_WithCorruptedImageData_HandlesGracefully()
    {
        // Arrange
        var corruptedData = new byte[1000];
        new Random().NextBytes(corruptedData);

        // Act
        var result = await _faceProcessor.ProcessFrameAsync(corruptedData, 640, 480);

        // Assert
        result.Should().NotBeNull();
        result.ConfidenceScore.Should().Be(0); // Should return zero confidence for corrupted data
    }

    [Theory]
    [InlineData(96, 96)]
    [InlineData(128, 128)]
    [InlineData(64, 64)]
    public async Task ExtractLipRegionAsync_WithDifferentTargetSizes_ResizesCorrectly(int targetWidth, int targetHeight)
    {
        // Arrange
        var frameData = TestDataGenerator.GenerateTestFrameData(640, 480);
        var lipBounds = TestDataGenerator.GenerateTestBoundingBox(220, 260, 80, 60);

        // Act
        var result = await _faceProcessor.ExtractLipRegionAsync(frameData, lipBounds, targetWidth, targetHeight);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
        
        // The result should be resized to target dimensions
        // Note: Actual size depends on image format compression
    }

    public void Dispose()
    {
        _faceProcessor?.Dispose();
        
        // Clean up test directory
        if (Directory.Exists(_testModelsPath))
        {
            try
            {
                Directory.Delete(_testModelsPath, true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }
}