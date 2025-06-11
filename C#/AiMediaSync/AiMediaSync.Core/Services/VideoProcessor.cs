using AiMediaSync.Core.Interfaces;
using AiMediaSync.Core.Models;
using OpenCvSharp;
using Microsoft.Extensions.Logging;

namespace AiMediaSync.Core.Services;

/// <summary>
/// Video processing service implementation
/// </summary>
public class VideoProcessor : IVideoProcessor, IDisposable
{
    private readonly ILogger<VideoProcessor> _logger;
    private bool _disposed = false;

    public VideoProcessor(ILogger<VideoProcessor> logger)
    {
        _logger = logger;
    }

    public async Task<VideoMetadata> GetVideoMetadataAsync(string videoPath)
    {
        try
        {
            _logger.LogInformation($"Extracting metadata from video: {videoPath}");

            using var capture = new VideoCapture(videoPath);
            
            if (!capture.IsOpened())
            {
                throw new InvalidOperationException($"Cannot open video file: {videoPath}");
            }

            var metadata = new VideoMetadata
            {
                Width = (int)capture.Get(VideoCaptureProperties.FrameWidth),
                Height = (int)capture.Get(VideoCaptureProperties.FrameHeight),
                FrameRate = capture.Get(VideoCaptureProperties.Fps),
                TotalFrames = (int)capture.Get(VideoCaptureProperties.FrameCount),
                Codec = GetCodecName((int)capture.Get(VideoCaptureProperties.FourCC))
            };

            metadata.Duration = TimeSpan.FromSeconds(metadata.TotalFrames / metadata.FrameRate);

            _logger.LogInformation($"Video metadata extracted: {metadata.Width}x{metadata.Height}, {metadata.FrameRate} FPS, {metadata.TotalFrames} frames");

            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error extracting video metadata from {videoPath}");
            throw;
        }
    }

    public async Task<byte[][]> ExtractFramesAsync(string videoPath)
    {
        try
        {
            _logger.LogInformation($"Extracting frames from video: {videoPath}");

            using var capture = new VideoCapture(videoPath);
            
            if (!capture.IsOpened())
            {
                throw new InvalidOperationException($"Cannot open video file: {videoPath}");
            }

            var frames = new List<byte[]>();
            using var frame = new Mat();
            
            while (true)
            {
                if (!capture.Read(frame) || frame.Empty())
                    break;

                var frameData = frame.ToBytes(".jpg");
                frames.Add(frameData);
            }

            _logger.LogInformation($"Extracted {frames.Count} frames from video");
            return frames.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error extracting frames from {videoPath}");
            throw;
        }
    }

    public async Task<bool> SaveVideoAsync(byte[][] frames, string outputPath, VideoMetadata metadata)
    {
        try
        {
            _logger.LogInformation($"Saving {frames.Length} frames to video: {outputPath}");

            if (frames.Length == 0)
            {
                _logger.LogWarning("No frames to save");
                return false;
            }

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Determine output format based on file extension
            var extension = Path.GetExtension(outputPath).ToLower();
            var fourcc = GetFourCCFromExtension(extension);

            using var writer = new VideoWriter(outputPath, fourcc, metadata.FrameRate, 
                new Size(metadata.Width, metadata.Height), true);

            if (!writer.IsOpened())
            {
                throw new InvalidOperationException($"Cannot create video writer for: {outputPath}");
            }

            foreach (var frameData in frames)
            {
                using var frame = Mat.FromImageData(frameData);
                
                // Ensure frame matches expected dimensions
                if (frame.Width != metadata.Width || frame.Height != metadata.Height)
                {
                    using var resized = new Mat();
                    Cv2.Resize(frame, resized, new Size(metadata.Width, metadata.Height));
                    writer.Write(resized);
                }
                else
                {
                    writer.Write(frame);
                }
            }

            _logger.LogInformation($"Video saved successfully to: {outputPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error saving video to {outputPath}");
            return false;
        }
    }

    public async Task<byte[]> ResizeFrameAsync(byte[] frameData, int currentWidth, int currentHeight, 
        int targetWidth, int targetHeight)
    {
        try
        {
            using var mat = Mat.FromImageData(frameData);
            
            if (mat.Width == targetWidth && mat.Height == targetHeight)
            {
                return frameData;
            }

            using var resized = new Mat();
            Cv2.Resize(mat, resized, new Size(targetWidth, targetHeight));
            
            return resized.ToBytes(".jpg");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resizing frame");
            throw;
        }
    }

    private string GetCodecName(int fourcc)
    {
        var bytes = BitConverter.GetBytes(fourcc);
        return System.Text.Encoding.ASCII.GetString(bytes).TrimEnd('\0');
    }

    private int GetFourCCFromExtension(string extension)
    {
        return extension switch
        {
            ".mp4" => VideoWriter.FourCC('m', 'p', '4', 'v'),
            ".avi" => VideoWriter.FourCC('X', 'V', 'I', 'D'),
            ".mov" => VideoWriter.FourCC('m', 'p', '4', 'v'),
            ".mkv" => VideoWriter.FourCC('X', '2', '6', '4'),
            _ => VideoWriter.FourCC('m', 'p', '4', 'v')
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}