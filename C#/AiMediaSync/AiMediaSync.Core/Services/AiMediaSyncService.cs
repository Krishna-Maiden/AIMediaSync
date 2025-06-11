using AiMediaSync.Core.Interfaces;
using AiMediaSync.Core.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AiMediaSync.Core.Services;

/// <summary>
/// Main orchestrator service for AiMediaSync processing
/// </summary>
public class AiMediaSyncService : IDisposable
{
    private readonly IAudioProcessor _audioProcessor;
    private readonly IFaceProcessor _faceProcessor;
    private readonly IVideoProcessor _videoProcessor;
    private readonly ILipSyncModel _lipSyncModel;
    private readonly IGuidanceSystem _guidanceSystem;
    private readonly ILogger<AiMediaSyncService> _logger;
    private bool _disposed = false;

    public AiMediaSyncService(
        IAudioProcessor audioProcessor,
        IFaceProcessor faceProcessor,
        IVideoProcessor videoProcessor,
        ILipSyncModel lipSyncModel,
        IGuidanceSystem guidanceSystem,
        ILogger<AiMediaSyncService> logger)
    {
        _audioProcessor = audioProcessor;
        _faceProcessor = faceProcessor;
        _videoProcessor = videoProcessor;
        _lipSyncModel = lipSyncModel;
        _guidanceSystem = guidanceSystem;
        _logger = logger;
    }

    /// <summary>
    /// Process lip-sync for given video and audio files
    /// </summary>
    /// <param name="inputVideoPath">Path to input video file</param>
    /// <param name="inputAudioPath">Path to input audio file</param>
    /// <param name="outputVideoPath">Path for output video file</param>
    /// <param name="modelPath">Optional path to ONNX model file</param>
    /// <returns>Processing result</returns>
    public async Task<SyncResult> ProcessLipSyncAsync(string inputVideoPath, string inputAudioPath, 
        string outputVideoPath, string? modelPath = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new SyncResult();

        try
        {
            _logger.LogInformation($"Starting lip-sync processing: Video={inputVideoPath}, Audio={inputAudioPath}");

            // Validate inputs
            if (!File.Exists(inputVideoPath))
                throw new FileNotFoundException($"Input video not found: {inputVideoPath}");
            
            if (!File.Exists(inputAudioPath))
                throw new FileNotFoundException($"Input audio not found: {inputAudioPath}");

            // Load model if specified
            if (!string.IsNullOrEmpty(modelPath) && !await _lipSyncModel.IsModelLoadedAsync())
            {
                await _lipSyncModel.LoadModelAsync(modelPath);
            }

            // Step 1: Extract video metadata and frames
            _logger.LogInformation("Extracting video frames and metadata...");
            var videoMetadata = await _videoProcessor.GetVideoMetadataAsync(inputVideoPath);
            var videoFrames = await _videoProcessor.ExtractFramesAsync(inputVideoPath);

            // Step 2: Process audio
            _logger.LogInformation("Processing audio features...");
            var audioData = await _audioProcessor.LoadAudioAsync(inputAudioPath);
            var audioFeatures = await _audioProcessor.ExtractFeaturesAsync(audioData, 16000);
            var alignedAudio = await _audioProcessor.AlignAudioWithVideoAsync(
                audioFeatures, videoFrames.Length, videoMetadata.FrameRate);

            // Step 3: Process each frame
            _logger.LogInformation($"Processing {videoFrames.Length} frames...");
            var processedFrames = new byte[videoFrames.Length][];
            var metrics = new SyncMetrics
            {
                TotalFrames = videoFrames.Length,
                ProcessedFrames = 0
            };

            float totalConfidence = 0;
            var frameProcessingTimes = new List<long>();

            for (int i = 0; i < videoFrames.Length; i++)
            {
                var frameStopwatch = Stopwatch.StartNew();

                try
                {
                    // Process frame
                    var frameData = videoFrames[i];
                    var visualFeatures = await _faceProcessor.ProcessFrameAsync(
                        frameData, videoMetadata.Width, videoMetadata.Height);

                    if (visualFeatures.ConfidenceScore > 0.3f) // Only process if face detected
                    {
                        // Get aligned audio features for this frame
                        var frameAudioFeatures = ExtractFrameAudioFeatures(alignedAudio, i);

                        // Compute guidance strength
                        var audioPower = ComputeAudioPower(frameAudioFeatures);
                        var guidanceStrength = _guidanceSystem.ComputeGuidanceStrength(
                            audioPower, i, videoFrames.Length);

                        // Generate lip-sync (if model is loaded)
                        if (await _lipSyncModel.IsModelLoadedAsync())
                        {
                            var lipMotion = await _lipSyncModel.GenerateLipMotionAsync(
                                frameAudioFeatures, visualFeatures.FaceEmbedding);
                            
                            // Apply lip motion to frame (placeholder)
                            processedFrames[i] = await ApplyLipMotionToFrame(
                                frameData, lipMotion, visualFeatures, guidanceStrength);
                        }
                        else
                        {
                            // No model loaded, return original frame
                            processedFrames[i] = frameData;
                        }

                        totalConfidence += visualFeatures.ConfidenceScore;
                        metrics.ProcessedFrames++;
                    }
                    else
                    {
                        // Low confidence, keep original frame
                        processedFrames[i] = frameData;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Error processing frame {i}, using original");
                    processedFrames[i] = videoFrames[i];
                }

                frameStopwatch.Stop();
                frameProcessingTimes.Add(frameStopwatch.ElapsedMilliseconds);

                // Progress logging
                if ((i + 1) % 100 == 0 || i == videoFrames.Length - 1)
                {
                    _logger.LogInformation($"Processed {i + 1}/{videoFrames.Length} frames");
                }
            }

            // Step 4: Save output video
            _logger.LogInformation("Saving output video...");
            var saveSuccess = await _videoProcessor.SaveVideoAsync(processedFrames, outputVideoPath, videoMetadata);

            if (!saveSuccess)
            {
                throw new InvalidOperationException("Failed to save output video");
            }

            // Calculate metrics
            metrics.AverageConfidence = metrics.ProcessedFrames > 0 ? totalConfidence / metrics.ProcessedFrames : 0;
            metrics.AverageFrameProcessingTime = TimeSpan.FromMilliseconds(
                frameProcessingTimes.Count > 0 ? frameProcessingTimes.Average() : 0);
            metrics.AudioVideoAlignment = ComputeAlignmentScore(audioFeatures, videoFrames.Length);

            stopwatch.Stop();

            result = new SyncResult
            {
                IsSuccess = true,
                OutputPath = outputVideoPath,
                ProcessingTime = stopwatch.Elapsed,
                QualityScore = metrics.AverageConfidence * 100,
                Metrics = metrics
            };

            _logger.LogInformation($"Lip-sync processing completed successfully in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during lip-sync processing");
            
            result = new SyncResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                ProcessingTime = stopwatch.Elapsed
            };
            
            return result;
        }
    }

    /// <summary>
    /// Extract audio features for a specific frame
    /// </summary>
    /// <param name="alignedAudio">Aligned audio features</param>
    /// <param name="frameIndex">Frame index</param>
    /// <returns>Audio features for the frame</returns>
    private float[] ExtractFrameAudioFeatures(float[,] alignedAudio, int frameIndex)
    {
        var featureCount = alignedAudio.GetLength(1);
        var frameFeatures = new float[featureCount];
        
        for (int i = 0; i < featureCount; i++)
        {
            frameFeatures[i] = alignedAudio[frameIndex, i];
        }
        
        return frameFeatures;
    }

    /// <summary>
    /// Compute audio power from features
    /// </summary>
    /// <param name="audioFeatures">Audio feature vector</param>
    /// <returns>Audio power level</returns>
    private float ComputeAudioPower(float[] audioFeatures)
    {
        var power = 0.0f;
        foreach (var feature in audioFeatures)
        {
            power += feature * feature;
        }
        return (float)Math.Sqrt(power / audioFeatures.Length);
    }

    /// <summary>
    /// Apply lip motion to video frame (placeholder implementation)
    /// </summary>
    /// <param name="originalFrame">Original frame data</param>
    /// <param name="lipMotion">Lip motion parameters</param>
    /// <param name="visualFeatures">Visual features</param>
    /// <param name="guidanceStrength">Guidance strength</param>
    /// <returns>Modified frame data</returns>
    private async Task<byte[]> ApplyLipMotionToFrame(byte[] originalFrame, float[] lipMotion, 
        VisualFeatures visualFeatures, float guidanceStrength)
    {
        // Placeholder implementation for applying lip motion to frame
        // In production, this would involve sophisticated image synthesis
        
        try
        {
            // For now, return original frame
            // In a real implementation, this would:
            // 1. Extract lip region from original frame
            // 2. Apply lip motion transformations
            // 3. Blend the modified lip region back into the frame
            // 4. Apply temporal smoothing and guidance scaling
            
            _logger.LogDebug($"Applied lip motion with guidance strength: {guidanceStrength:F3}");
            return originalFrame;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error applying lip motion, returning original frame");
            return originalFrame;
        }
    }

    /// <summary>
    /// Compute alignment quality score between audio and video
    /// </summary>
    /// <param name="audioFeatures">Audio features</param>
    /// <param name="videoFrameCount">Number of video frames</param>
    /// <returns>Alignment score (0-1)</returns>
    private float ComputeAlignmentScore(AudioFeatures audioFeatures, int videoFrameCount)
    {
        // Compute alignment quality score between audio and video
        var expectedFrames = (int)(audioFeatures.AudioLength * 25); // Assuming 25 FPS
        var frameDifference = Math.Abs(videoFrameCount - expectedFrames);
        var alignmentScore = Math.Max(0, 1.0f - (frameDifference / (float)videoFrameCount));
        
        return alignmentScore;
    }

    /// <summary>
    /// Validate input files and parameters
    /// </summary>
    /// <param name="videoPath">Video file path</param>
    /// <param name="audioPath">Audio file path</param>
    /// <param name="outputPath">Output file path</param>
    /// <returns>True if valid</returns>
    public bool ValidateInputs(string videoPath, string audioPath, string outputPath)
    {
        try
        {
            // Check video file
            if (!File.Exists(videoPath))
            {
                _logger.LogError($"Video file not found: {videoPath}");
                return false;
            }

            var videoExtension = Path.GetExtension(videoPath).ToLower();
            var supportedVideoFormats = new[] { ".mp4", ".avi", ".mov", ".mkv" };
            if (!supportedVideoFormats.Contains(videoExtension))
            {
                _logger.LogError($"Unsupported video format: {videoExtension}");
                return false;
            }

            // Check audio file
            if (!File.Exists(audioPath))
            {
                _logger.LogError($"Audio file not found: {audioPath}");
                return false;
            }

            var audioExtension = Path.GetExtension(audioPath).ToLower();
            var supportedAudioFormats = new[] { ".wav", ".mp3", ".aac", ".flac" };
            if (!supportedAudioFormats.Contains(audioExtension))
            {
                _logger.LogError($"Unsupported audio format: {audioExtension}");
                return false;
            }

            // Check output path
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                try
                {
                    Directory.CreateDirectory(outputDir);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Cannot create output directory: {outputDir}");
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating inputs");
            return false;
        }
    }

    /// <summary>
    /// Get processing statistics
    /// </summary>
    /// <returns>System statistics</returns>
    public async Task<Dictionary<string, object>> GetSystemStatisticsAsync()
    {
        var stats = new Dictionary<string, object>();

        try
        {
            // System information
            stats["MachineName"] = Environment.MachineName;
            stats["ProcessorCount"] = Environment.ProcessorCount;
            stats["OSVersion"] = Environment.OSVersion.ToString();
            stats["WorkingSet"] = Environment.WorkingSet;

            // Service status
            stats["AudioProcessorReady"] = _audioProcessor != null;
            stats["FaceProcessorReady"] = _faceProcessor != null;
            stats["VideoProcessorReady"] = _videoProcessor != null;
            stats["ModelLoaded"] = await _lipSyncModel.IsModelLoadedAsync();

            // Performance metrics
            var process = Process.GetCurrentProcess();
            stats["MemoryUsage"] = process.WorkingSet64;
            stats["CpuTime"] = process.TotalProcessorTime.TotalMilliseconds;

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting system statistics");
            return stats;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _faceProcessor?.Dispose();
            _videoProcessor?.Dispose();
            _lipSyncModel?.Dispose();
            _disposed = true;
        }
    }
}