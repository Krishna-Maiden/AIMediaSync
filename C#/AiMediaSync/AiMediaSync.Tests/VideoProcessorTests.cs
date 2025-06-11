using AiMediaSync.Core.Services;
using AiMediaSync.Core.Models;
using AiMediaSync.Tests.TestUtilities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace AiMediaSync.Tests;

public class VideoProcessorTests : IDisposable
{
    private readonly Mock<ILogger<VideoProcessor>> _loggerMock;
    private readonly VideoProcessor _videoProcessor;
    private readonly string _testDataPath;

    public VideoProcessorTests()
    {
        _loggerMock = MockExtensions.CreateMockLogger<VideoProcessor>();
        _videoProcessor = new VideoProcessor(_loggerMock.Object);
        _testDataPath = Path.Combine("TestData", "Videos");
        
        // Create test data directory
        Directory.CreateDirectory(_testDataPath);
    }

    [Fact]
    public async Task GetVideoMetadataAsync_WithValidVideo_ReturnsMetadata()
    {
        // Arrange
        var testVideoPath = await CreateTestVideoFileAsync();

        // Act
        var result = await _videoProcessor.GetVideoMetadataAsync(testVideoPath);

        // Assert
        result.Should().NotBeNull();
        result.Width.Should().BeGreaterThan(0);
        result.Height.Should().BeGreaterThan(0);
        result.FrameRate.Should().BeGreaterThan(0);
        result.TotalFrames.Should().BeGreaterThan(0);
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task GetVideoMetadataAsync_WithNonExistentFile_ThrowsException()
    {
        // Arrange
        var nonExistentPath = "non_existent_video.mp4";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _videoProcessor.GetVideoMetadataAsync(nonExistentPath));
    }

    [Fact]
    public async Task ExtractFramesAsync_WithValidVideo_ReturnsFrames()
    {
        // Arrange
        var testVideoPath = await CreateTestVideoFileAsync();

        // Act
        var result = await _videoProcessor.ExtractFramesAsync(testVideoPath);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
        result.Should().AllSatisfy(frame => frame.Should().NotBeNull());
        result.Should().AllSatisfy(frame => frame.Length.Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task ExtractFramesAsync_WithCorruptedVideo_HandlesGracefully()
    {
        // Arrange
        var corruptedVideoPath = Path.Combine(_testDataPath, "corrupted.mp4");
        await File.WriteAllBytesAsync(corruptedVideoPath, new byte[] { 0x00, 0x01, 0x02, 0x03 });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _videoProcessor.ExtractFramesAsync(corruptedVideoPath));
    }

    [Fact]
    public async Task SaveVideoAsync_WithValidFrames_ReturnsTrue()
    {
        // Arrange
        var frames = TestDataGenerator.GenerateTestFrames(25, 640, 480);
        var outputPath = Path.Combine(_testDataPath, "output_test.mp4");
        var metadata = TestDataGenerator.GenerateTestVideoMetadata(640, 480, 25.0, 25);

        // Act
        var result = await _videoProcessor.SaveVideoAsync(frames, outputPath, metadata);

        // Assert
        result.Should().BeTrue();
        File.Exists(outputPath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveVideoAsync_WithEmptyFrames_ReturnsFalse()
    {
        // Arrange
        var emptyFrames = new byte[0][];
        var outputPath = Path.Combine(_testDataPath, "empty_output.mp4");
        var metadata = TestDataGenerator.GenerateTestVideoMetadata();

        // Act
        var result = await _videoProcessor.SaveVideoAsync(emptyFrames, outputPath, metadata);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SaveVideoAsync_WithInvalidOutputPath_ReturnsFalse()
    {
        // Arrange
        var frames = TestDataGenerator.GenerateTestFrames(10);
        var invalidPath = "/invalid/path/that/does/not/exist/video.mp4";
        var metadata = TestDataGenerator.GenerateTestVideoMetadata();

        // Act
        var result = await _videoProcessor.SaveVideoAsync(frames, invalidPath, metadata);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ResizeFrameAsync_WithValidFrame_ReturnsResizedFrame()
    {
        // Arrange
        var originalFrame = TestDataGenerator.GenerateTestFrameData(640, 480);
        const int currentWidth = 640;
        const int currentHeight = 480;
        const int targetWidth = 320;
        const int targetHeight = 240;

        // Act
        var result = await _videoProcessor.ResizeFrameAsync(
            originalFrame, currentWidth, currentHeight, targetWidth, targetHeight);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
        result.Length.Should().BeLessThan(originalFrame.Length); // Resized frame should be smaller
    }

    [Fact]
    public async Task ResizeFrameAsync_WithSameDimensions_ReturnsOriginalFrame()
    {
        // Arrange
        var originalFrame = TestDataGenerator.GenerateTestFrameData(640, 480);
        const int width = 640;
        const int height = 480;

        // Act
        var result = await _videoProcessor.ResizeFrameAsync(
            originalFrame, width, height, width, height);

        // Assert
        result.Should().NotBeNull();
        result.Should().Equal(originalFrame);
    }

    [Theory]
    [InlineData(1920, 1080, 960, 540)]  // Half size
    [InlineData(640, 480, 1280, 960)]   // Double size
    [InlineData(1280, 720, 854, 480)]   // Different aspect ratio
    public async Task ResizeFrameAsync_WithDifferentDimensions_HandlesCorrectly(
        int originalWidth, int originalHeight, int targetWidth, int targetHeight)
    {
        // Arrange
        var originalFrame = TestDataGenerator.GenerateTestFrameData(originalWidth, originalHeight);

        // Act
        var result = await _videoProcessor.ResizeFrameAsync(
            originalFrame, originalWidth, originalHeight, targetWidth, targetHeight);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SaveVideoAsync_WithDifferentCodecs_HandlesCorrectly()
    {
        // Arrange
        var frames = TestDataGenerator.GenerateTestFrames(10);
        var metadata = TestDataGenerator.GenerateTestVideoMetadata();
        
        var testCases = new[]
        {
            ("output_mp4.mp4", metadata),
            ("output_avi.avi", metadata),
            ("output_mov.mov", metadata)
        };

        foreach (var (fileName, meta) in testCases)
        {
            var outputPath = Path.Combine(_testDataPath, fileName);

            // Act
            var result = await _videoProcessor.SaveVideoAsync(frames, outputPath, meta);

            // Assert
            result.Should().BeTrue($"Failed to save {fileName}");
            
            if (result)
            {
                File.Exists(outputPath).Should().BeTrue($"Output file {fileName} was not created");
            }
        }
    }

    [Fact]
    public async Task ExtractFramesAsync_WithLargeVideo_HandlesMemoryEfficiently()
    {
        // Arrange
        var testVideoPath = await CreateTestVideoFileAsync(100); // 100 frames

        // Act
        var result = await _videoProcessor.ExtractFramesAsync(testVideoPath);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
        
        // Check memory usage doesn't explode
        var totalMemory = GC.GetTotalMemory(false);
        totalMemory.Should().BeLessThan(500 * 1024 * 1024); // Less than 500MB
    }

    [Fact]
    public async Task SaveVideoAsync_ConcurrentCalls_HandlesCorrectly()
    {
        // Arrange
        var frames1 = TestDataGenerator.GenerateTestFrames(10);
        var frames2 = TestDataGenerator.GenerateTestFrames(15);
        var frames3 = TestDataGenerator.GenerateTestFrames(20);
        
        var metadata = TestDataGenerator.GenerateTestVideoMetadata();
        
        var path1 = Path.Combine(_testDataPath, "concurrent1.mp4");
        var path2 = Path.Combine(_testDataPath, "concurrent2.mp4");
        var path3 = Path.Combine(_testDataPath, "concurrent3.mp4");

        // Act
        var tasks = new[]
        {
            _videoProcessor.SaveVideoAsync(frames1, path1, metadata),
            _videoProcessor.SaveVideoAsync(frames2, path2, metadata),
            _videoProcessor.SaveVideoAsync(frames3, path3, metadata)
        };

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeTrue());
        File.Exists(path1).Should().BeTrue();
        File.Exists(path2).Should().BeTrue();
        File.Exists(path3).Should().BeTrue();
    }

    [Fact]
    public async Task GetVideoMetadataAsync_WithDifferentVideoFormats_ReturnsCorrectData()
    {
        // Arrange
        var testVideoPath = await CreateTestVideoFileAsync();

        // Act
        var result = await _videoProcessor.GetVideoMetadataAsync(testVideoPath);

        // Assert
        result.Should().NotBeNull();
        result.Width.Should().BeInRange(1, 4096);
        result.Height.Should().BeInRange(1, 2160);
        result.FrameRate.Should().BeInRange(1, 120);
        result.TotalFrames.Should().BeGreaterThan(0);
        result.Duration.TotalSeconds.Should().BeGreaterThan(0);
        result.Codec.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ResizeFrameAsync_WithInvalidDimensions_ThrowsException()
    {
        // Arrange
        var frame = TestDataGenerator.GenerateTestFrameData(640, 480);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _videoProcessor.ResizeFrameAsync(frame, 640, 480, 0, 0));
        
        await Assert.ThrowsAsync<ArgumentException>(
            () => _videoProcessor.ResizeFrameAsync(frame, 640, 480, -100, 100));
    }

    [Fact]
    public async Task SaveVideoAsync_WithMismatchedFramesDimensions_HandlesCorrectly()
    {
        // Arrange
        var frames = new[]
        {
            TestDataGenerator.GenerateTestFrameData(640, 480),
            TestDataGenerator.GenerateTestFrameData(1280, 720), // Different size
            TestDataGenerator.GenerateTestFrameData(640, 480)
        };
        
        var outputPath = Path.Combine(_testDataPath, "mismatched_output.mp4");
        var metadata = TestDataGenerator.GenerateTestVideoMetadata(640, 480);

        // Act
        var result = await _videoProcessor.SaveVideoAsync(frames, outputPath, metadata);

        // Assert
        result.Should().BeTrue(); // Should handle by resizing frames to match metadata
    }

    [Fact]
    public void VideoProcessor_Initialization_CreatesSuccessfully()
    {
        // Arrange & Act
        var processor = new VideoProcessor(_loggerMock.Object);

        // Assert
        processor.Should().NotBeNull();
    }

    private async Task<string> CreateTestVideoFileAsync(int frameCount = 25)
    {
        // Create a minimal valid MP4 file for testing
        var testVideoPath = Path.Combine(_testDataPath, $"test_video_{Guid.NewGuid():N}.mp4");
        
        // Create a minimal MP4 file structure
        var mp4Header = new byte[]
        {
            // Minimal MP4 header that OpenCV can recognize
            0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70, // ftyp box
            0x69, 0x73, 0x6F, 0x6D, 0x00, 0x00, 0x02, 0x00,
            0x69, 0x73, 0x6F, 0x6D, 0x69, 0x73, 0x6F, 0x32,
            0x61, 0x76, 0x63, 0x31, 0x6D, 0x70, 0x34, 0x31
        };
        
        // For testing purposes, create a file that simulates a video
        // In a real test environment, you would use actual video files
        var testData = new byte[1024 * frameCount]; // 1KB per "frame"
        new Random(42).NextBytes(testData);
        
        var combinedData = mp4Header.Concat(testData).ToArray();
        await File.WriteAllBytesAsync(testVideoPath, combinedData);
        
        return testVideoPath;
    }

    public void Dispose()
    {
        _videoProcessor?.Dispose();
        
        // Clean up test directory
        if (Directory.Exists(_testDataPath))
        {
            try
            {
                Directory.Delete(_testDataPath, true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }
}