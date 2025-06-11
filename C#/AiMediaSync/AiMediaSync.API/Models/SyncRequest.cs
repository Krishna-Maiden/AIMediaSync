using System.ComponentModel.DataAnnotations;

namespace AiMediaSync.API.Models;

/// <summary>
/// Request model for lip-sync processing
/// </summary>
public class SyncRequest
{
    /// <summary>
    /// Video file to process
    /// </summary>
    [Required(ErrorMessage = "Video file is required")]
    public IFormFile VideoFile { get; set; } = null!;
    
    /// <summary>
    /// Audio file for synchronization
    /// </summary>
    [Required(ErrorMessage = "Audio file is required")]
    public IFormFile AudioFile { get; set; } = null!;
    
    /// <summary>
    /// Optional path to custom ONNX model
    /// </summary>
    public string? ModelPath { get; set; }
    
    /// <summary>
    /// Quality threshold for processing (0.0 to 1.0)
    /// </summary>
    [Range(0.0, 1.0, ErrorMessage = "Quality threshold must be between 0.0 and 1.0")]
    public float QualityThreshold { get; set; } = 0.8f;
    
    /// <summary>
    /// Enable GPU acceleration for processing
    /// </summary>
    public bool EnableGpuAcceleration { get; set; } = true;
    
    /// <summary>
    /// Maximum processing timeout in minutes
    /// </summary>
    [Range(1, 60, ErrorMessage = "Timeout must be between 1 and 60 minutes")]
    public int TimeoutMinutes { get; set; } = 30;
    
    /// <summary>
    /// Priority level for processing queue
    /// </summary>
    public ProcessingPriority Priority { get; set; } = ProcessingPriority.Normal;
    
    /// <summary>
    /// Webhook URL for completion notification (optional)
    /// </summary>
    public string? WebhookUrl { get; set; }
    
    /// <summary>
    /// Custom parameters for processing
    /// </summary>
    public Dictionary<string, object>? CustomParameters { get; set; }
}

/// <summary>
/// Response model for lip-sync processing
/// </summary>
public class SyncResponse
{
    /// <summary>
    /// Unique request identifier
    /// </summary>
    public string RequestId { get; set; } = string.Empty;
    
    /// <summary>
    /// Job identifier for tracking
    /// </summary>
    public string JobId { get; set; } = string.Empty;
    
    /// <summary>
    /// Current processing status
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// URL to download the processed video
    /// </summary>
    public string OutputUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Quality score of the result (0-100)
    /// </summary>
    public float QualityScore { get; set; }
    
    /// <summary>
    /// Total processing time
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }
    
    /// <summary>
    /// Detailed processing metrics
    /// </summary>
    public object? Metrics { get; set; }
    
    /// <summary>
    /// Estimated file size in bytes
    /// </summary>
    public long? EstimatedFileSize { get; set; }
    
    /// <summary>
    /// Processing completion timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Expiration time for download link
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
    
    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Error response model
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error message
    /// </summary>
    public string Error { get; set; } = string.Empty;
    
    /// <summary>
    /// Detailed error description
    /// </summary>
    public string? Details { get; set; }
    
    /// <summary>
    /// Error timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Request identifier for tracking
    /// </summary>
    public string? RequestId { get; set; }
    
    /// <summary>
    /// Error code for programmatic handling
    /// </summary>
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// Suggested actions to resolve the error
    /// </summary>
    public string[]? SuggestedActions { get; set; }
    
    /// <summary>
    /// Support reference for error tracking
    /// </summary>
    public string? SupportReference { get; set; }
}

/// <summary>
/// Job status response model
/// </summary>
public class JobStatusResponse
{
    /// <summary>
    /// Job identifier
    /// </summary>
    public string JobId { get; set; } = string.Empty;
    
    /// <summary>
    /// Current job status
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Processing progress percentage (0-100)
    /// </summary>
    public int Progress { get; set; }
    
    /// <summary>
    /// Current processing stage
    /// </summary>
    public string? CurrentStage { get; set; }
    
    /// <summary>
    /// Estimated time remaining
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    
    /// <summary>
    /// Job creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// Processing start timestamp
    /// </summary>
    public DateTime? StartedAt { get; set; }
    
    /// <summary>
    /// Processing completion timestamp
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Output URL when completed
    /// </summary>
    public string? OutputUrl { get; set; }
    
    /// <summary>
    /// Quality metrics when available
    /// </summary>
    public object? Metrics { get; set; }
}

/// <summary>
/// Batch processing request model
/// </summary>
public class BatchSyncRequest
{
    /// <summary>
    /// List of sync requests to process
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one sync request is required")]
    [MaxLength(10, ErrorMessage = "Maximum 10 requests per batch")]
    public List<SyncRequest> Requests { get; set; } = new();
    
    /// <summary>
    /// Batch identifier for tracking
    /// </summary>
    public string? BatchId { get; set; }
    
    /// <summary>
    /// Priority for the entire batch
    /// </summary>
    public ProcessingPriority Priority { get; set; } = ProcessingPriority.Normal;
    
    /// <summary>
    /// Webhook URL for batch completion notification
    /// </summary>
    public string? WebhookUrl { get; set; }
    
    /// <summary>
    /// Whether to stop processing on first error
    /// </summary>
    public bool StopOnFirstError { get; set; } = false;
}

/// <summary>
/// Batch processing response model
/// </summary>
public class BatchSyncResponse
{
    /// <summary>
    /// Batch identifier
    /// </summary>
    public string BatchId { get; set; } = string.Empty;
    
    /// <summary>
    /// Overall batch status
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Individual job results
    /// </summary>
    public List<SyncResponse> Results { get; set; } = new();
    
    /// <summary>
    /// Batch creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Total processing time for batch
    /// </summary>
    public TimeSpan? TotalProcessingTime { get; set; }
    
    /// <summary>
    /// Number of successful jobs
    /// </summary>
    public int SuccessfulJobs { get; set; }
    
    /// <summary>
    /// Number of failed jobs
    /// </summary>
    public int FailedJobs { get; set; }
}

/// <summary>
/// Processing configuration model
/// </summary>
public class ProcessingConfig
{
    /// <summary>
    /// Video output quality setting
    /// </summary>
    public VideoQuality VideoQuality { get; set; } = VideoQuality.High;
    
    /// <summary>
    /// Audio processing sample rate
    /// </summary>
    public int AudioSampleRate { get; set; } = 16000;
    
    /// <summary>
    /// Maximum video resolution
    /// </summary>
    public VideoResolution MaxResolution { get; set; } = VideoResolution.HD1080;
    
    /// <summary>
    /// Enable advanced quality enhancement
    /// </summary>
    public bool EnableQualityEnhancement { get; set; } = true;
    
    /// <summary>
    /// Face detection confidence threshold
    /// </summary>
    public float FaceDetectionThreshold { get; set; } = 0.5f;
    
    /// <summary>
    /// Lip-sync quality threshold
    /// </summary>
    public float LipSyncThreshold { get; set; } = 0.8f;
    
    /// <summary>
    /// Custom processing parameters
    /// </summary>
    public Dictionary<string, object>? CustomParameters { get; set; }
}

#region Enums

/// <summary>
/// Processing priority levels
/// </summary>
public enum ProcessingPriority
{
    /// <summary>
    /// Low priority processing
    /// </summary>
    Low = 0,
    
    /// <summary>
    /// Normal priority processing
    /// </summary>
    Normal = 1,
    
    /// <summary>
    /// High priority processing
    /// </summary>
    High = 2,
    
    /// <summary>
    /// Critical priority processing
    /// </summary>
    Critical = 3
}

/// <summary>
/// Video quality settings
/// </summary>
public enum VideoQuality
{
    /// <summary>
    /// Low quality, fast processing
    /// </summary>
    Low = 0,
    
    /// <summary>
    /// Medium quality, balanced
    /// </summary>
    Medium = 1,
    
    /// <summary>
    /// High quality, slower processing
    /// </summary>
    High = 2,
    
    /// <summary>
    /// Ultra high quality, slowest processing
    /// </summary>
    Ultra = 3
}

/// <summary>
/// Video resolution options
/// </summary>
public enum VideoResolution
{
    /// <summary>
    /// 480p resolution
    /// </summary>
    SD480 = 480,
    
    /// <summary>
    /// 720p HD resolution
    /// </summary>
    HD720 = 720,
    
    /// <summary>
    /// 1080p Full HD resolution
    /// </summary>
    HD1080 = 1080,
    
    /// <summary>
    /// 1440p Quad HD resolution
    /// </summary>
    QHD1440 = 1440,
    
    /// <summary>
    /// 2160p 4K resolution
    /// </summary>
    UHD4K = 2160
}

/// <summary>
/// Job status enumeration
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// Job is queued for processing
    /// </summary>
    Queued = 0,
    
    /// <summary>
    /// Job is currently being processed
    /// </summary>
    Processing = 1,
    
    /// <summary>
    /// Job completed successfully
    /// </summary>
    Completed = 2,
    
    /// <summary>
    /// Job failed with error
    /// </summary>
    Failed = 3,
    
    /// <summary>
    /// Job was cancelled
    /// </summary>
    Cancelled = 4,
    
    /// <summary>
    /// Job timed out
    /// </summary>
    TimedOut = 5
}

#endregion