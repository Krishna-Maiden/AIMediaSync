namespace AiMediaSync.Core.Models;

/// <summary>
/// Result of lip-sync processing operation
/// </summary>
public class SyncResult
{
    /// <summary>
    /// Indicates if the processing was successful
    /// </summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// Path to the output video file
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Total processing time
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }
    
    /// <summary>
    /// Quality score of the lip-sync result (0-100)
    /// </summary>
    public float QualityScore { get; set; }
    
    /// <summary>
    /// Error message if processing failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Detailed processing metrics
    /// </summary>
    public SyncMetrics? Metrics { get; set; }
}

/// <summary>
/// Detailed metrics from lip-sync processing
/// </summary>
public class SyncMetrics
{
    /// <summary>
    /// Total number of frames in the video
    /// </summary>
    public int TotalFrames { get; set; }
    
    /// <summary>
    /// Number of frames successfully processed
    /// </summary>
    public int ProcessedFrames { get; set; }
    
    /// <summary>
    /// Average confidence score across all frames
    /// </summary>
    public float AverageConfidence { get; set; }
    
    /// <summary>
    /// Audio-video alignment quality score
    /// </summary>
    public float AudioVideoAlignment { get; set; }
    
    /// <summary>
    /// Average processing time per frame
    /// </summary>
    public TimeSpan AverageFrameProcessingTime { get; set; }
}