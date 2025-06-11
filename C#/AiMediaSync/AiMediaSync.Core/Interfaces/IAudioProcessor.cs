using AiMediaSync.Core.Models;

namespace AiMediaSync.Core.Interfaces;

/// <summary>
/// Audio processing service interface
/// </summary>
public interface IAudioProcessor
{
    /// <summary>
    /// Load audio from file
    /// </summary>
    /// <param name="audioPath">Path to audio file</param>
    /// <returns>Audio data as float array</returns>
    Task<float[]> LoadAudioAsync(string audioPath);
    
    /// <summary>
    /// Extract features from audio data
    /// </summary>
    /// <param name="audioData">Raw audio data</param>
    /// <param name="sampleRate">Sample rate of the audio</param>
    /// <returns>Extracted audio features</returns>
    Task<AudioFeatures> ExtractFeaturesAsync(float[] audioData, int sampleRate = 16000);
    
    /// <summary>
    /// Resample audio to target sample rate
    /// </summary>
    /// <param name="audio">Original audio data</param>
    /// <param name="originalSampleRate">Original sample rate</param>
    /// <param name="targetSampleRate">Target sample rate</param>
    /// <returns>Resampled audio data</returns>
    Task<float[]> ResampleAudioAsync(float[] audio, int originalSampleRate, int targetSampleRate);
    
    /// <summary>
    /// Align audio features with video frames
    /// </summary>
    /// <param name="audioFeatures">Audio features to align</param>
    /// <param name="videoFrameCount">Number of video frames</param>
    /// <param name="videoFps">Video frame rate</param>
    /// <returns>Aligned audio features</returns>
    Task<float[,]> AlignAudioWithVideoAsync(AudioFeatures audioFeatures, int videoFrameCount, double videoFps);
}

/// <summary>
/// Face processing service interface
/// </summary>
public interface IFaceProcessor
{
    /// <summary>
    /// Process a video frame to extract visual features
    /// </summary>
    /// <param name="frameData">Frame image data</param>
    /// <param name="width">Frame width</param>
    /// <param name="height">Frame height</param>
    /// <returns>Extracted visual features</returns>
    Task<VisualFeatures> ProcessFrameAsync(byte[] frameData, int width, int height);
    
    /// <summary>
    /// Detect face in frame
    /// </summary>
    /// <param name="frameData">Frame image data</param>
    /// <param name="width">Frame width</param>
    /// <param name="height">Frame height</param>
    /// <returns>Face bounding box or null if no face detected</returns>
    Task<BoundingBox?> DetectFaceAsync(byte[] frameData, int width, int height);
    
    /// <summary>
    /// Extract facial landmarks
    /// </summary>
    /// <param name="frameData">Frame image data</param>
    /// <param name="faceBounds">Face bounding box</param>
    /// <returns>Facial landmark points</returns>
    Task<LandmarkPoints> ExtractLandmarksAsync(byte[] frameData, BoundingBox faceBounds);
    
    /// <summary>
    /// Extract lip region from frame
    /// </summary>
    /// <param name="frameData">Frame image data</param>
    /// <param name="lipBounds">Lip region bounding box</param>
    /// <param name="targetWidth">Target width for lip region</param>
    /// <param name="targetHeight">Target height for lip region</param>
    /// <returns>Lip region image data</returns>
    Task<byte[]> ExtractLipRegionAsync(byte[] frameData, BoundingBox lipBounds, int targetWidth = 96, int targetHeight = 96);
}

/// <summary>
/// Lip-sync model interface
/// </summary>
public interface ILipSyncModel
{
    /// <summary>
    /// Predict lip motion from audio and visual features
    /// </summary>
    /// <param name="audioFeatures">Audio feature vector</param>
    /// <param name="visualFeatures">Visual feature vector</param>
    /// <returns>Predicted lip motion parameters</returns>
    Task<float[]> PredictAsync(float[] audioFeatures, float[] visualFeatures);
    
    /// <summary>
    /// Load ONNX model from file
    /// </summary>
    /// <param name="modelPath">Path to ONNX model file</param>
    Task LoadModelAsync(string modelPath);
    
    /// <summary>
    /// Check if model is loaded and ready
    /// </summary>
    /// <returns>True if model is loaded</returns>
    Task<bool> IsModelLoadedAsync();
    
    /// <summary>
    /// Generate lip motion from audio and identity features
    /// </summary>
    /// <param name="audioFeatures">Audio feature vector</param>
    /// <param name="identityFeatures">Identity feature vector</param>
    /// <returns>Generated lip motion parameters</returns>
    Task<float[]> GenerateLipMotionAsync(float[] audioFeatures, float[] identityFeatures);
}

/// <summary>
/// Video processing service interface
/// </summary>
public interface IVideoProcessor
{
    /// <summary>
    /// Get video metadata
    /// </summary>
    /// <param name="videoPath">Path to video file</param>
    /// <returns>Video metadata</returns>
    Task<VideoMetadata> GetVideoMetadataAsync(string videoPath);
    
    /// <summary>
    /// Extract all frames from video
    /// </summary>
    /// <param name="videoPath">Path to video file</param>
    /// <returns>Array of frame image data</returns>
    Task<byte[][]> ExtractFramesAsync(string videoPath);
    
    /// <summary>
    /// Save frames as video file
    /// </summary>
    /// <param name="frames">Array of frame image data</param>
    /// <param name="outputPath">Output video file path</param>
    /// <param name="metadata">Video metadata</param>
    /// <returns>True if successful</returns>
    Task<bool> SaveVideoAsync(byte[][] frames, string outputPath, VideoMetadata metadata);
    
    /// <summary>
    /// Resize frame to target dimensions
    /// </summary>
    /// <param name="frameData">Original frame data</param>
    /// <param name="currentWidth">Current frame width</param>
    /// <param name="currentHeight">Current frame height</param>
    /// <param name="targetWidth">Target width</param>
    /// <param name="targetHeight">Target height</param>
    /// <returns>Resized frame data</returns>
    Task<byte[]> ResizeFrameAsync(byte[] frameData, int currentWidth, int currentHeight, int targetWidth, int targetHeight);
}

/// <summary>
/// Dynamic guidance system interface
/// </summary>
public interface IGuidanceSystem
{
    /// <summary>
    /// Compute guidance strength for adaptive lip-sync
    /// </summary>
    /// <param name="audioPower">Current audio power level</param>
    /// <param name="frameIndex">Current frame index</param>
    /// <param name="totalFrames">Total number of frames</param>
    /// <param name="baseStrength">Base guidance strength</param>
    /// <returns>Computed guidance strength</returns>
    float ComputeGuidanceStrength(float audioPower, int frameIndex, int totalFrames, float baseStrength = 0.7f);
    
    /// <summary>
    /// Compute temporal consistency between frames
    /// </summary>
    /// <param name="currentFeatures">Current frame features</param>
    /// <param name="previousFeatures">Previous frame features</param>
    /// <returns>Temporal consistency score</returns>
    float ComputeTemporalConsistency(float[] currentFeatures, float[] previousFeatures);
    
    /// <summary>
    /// Adaptive guidance scaling based on content complexity
    /// </summary>
    /// <param name="audioEnergy">Audio energy level</param>
    /// <param name="visualComplexity">Visual complexity measure</param>
    /// <returns>Scaling factor for guidance</returns>
    float AdaptiveGuidanceScaling(float audioEnergy, float visualComplexity);
}