using Microsoft.AspNetCore.Mvc;
using AiMediaSync.Core.Services;
using AiMediaSync.API.Models;
using System.ComponentModel.DataAnnotations;

namespace AiMediaSync.API.Controllers;

/// <summary>
/// Lip synchronization processing controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SyncController : ControllerBase
{
    private readonly AiMediaSyncService _syncService;
    private readonly ILogger<SyncController> _logger;
    private readonly IConfiguration _configuration;

    public SyncController(
        AiMediaSyncService syncService, 
        ILogger<SyncController> logger,
        IConfiguration configuration)
    {
        _syncService = syncService;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Process lip-sync for uploaded video and audio files
    /// </summary>
    /// <param name="request">Sync processing request with files</param>
    /// <returns>Processing result with output information</returns>
    /// <response code="200">Processing completed successfully</response>
    /// <response code="400">Invalid request or file format</response>
    /// <response code="413">File size exceeds limit</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("process")]
    [ProducesResponseType(typeof(SyncResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 413)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<ActionResult<SyncResponse>> ProcessLipSync([FromForm] SyncRequest request)
    {
        var requestId = Guid.NewGuid().ToString();
        _logger.LogInformation("Processing lip-sync request {RequestId}", requestId);

        try
        {
            // Validate request
            var validationResult = ValidateRequest(request);
            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorResponse 
                { 
                    Error = "Invalid request",
                    Details = validationResult.ErrorMessage,
                    RequestId = requestId
                });
            }

            // Check file sizes
            var maxFileSize = _configuration.GetValue<long>("AiMediaSync:MaxFileSizeBytes", 1073741824); // 1GB default
            if (request.VideoFile.Length > maxFileSize || request.AudioFile.Length > maxFileSize)
            {
                return StatusCode(413, new ErrorResponse 
                { 
                    Error = "File size exceeds limit",
                    Details = $"Maximum file size is {maxFileSize / (1024 * 1024)}MB",
                    RequestId = requestId
                });
            }

            // Save uploaded files
            var tempDir = Path.Combine(_configuration["AiMediaSync:TempPath"] ?? "Temp", requestId);
            Directory.CreateDirectory(tempDir);

            var videoPath = await SaveUploadedFile(request.VideoFile, tempDir, "video");
            var audioPath = await SaveUploadedFile(request.AudioFile, tempDir, "audio");
            
            var outputDir = _configuration["AiMediaSync:OutputPath"] ?? "Output";
            Directory.CreateDirectory(outputDir);
            var outputPath = Path.Combine(outputDir, $"synced_{requestId}.mp4");

            _logger.LogInformation("Files saved for request {RequestId}: Video={VideoPath}, Audio={AudioPath}", 
                requestId, videoPath, audioPath);

            // Validate input files
            if (!_syncService.ValidateInputs(videoPath, audioPath, outputPath))
            {
                return BadRequest(new ErrorResponse 
                { 
                    Error = "Invalid input files",
                    Details = "Please check file formats and try again",
                    RequestId = requestId
                });
            }

            // Process lip-sync
            var result = await _syncService.ProcessLipSyncAsync(videoPath, audioPath, outputPath, request.ModelPath);

            if (result.IsSuccess)
            {
                var response = new SyncResponse
                {
                    RequestId = requestId,
                    JobId = requestId, // In a real implementation, this might be different
                    Status = "Completed",
                    OutputUrl = $"/api/sync/download/{Path.GetFileName(outputPath)}",
                    QualityScore = result.QualityScore,
                    ProcessingTime = result.ProcessingTime,
                    Metrics = result.Metrics,
                    CreatedAt = DateTime.UtcNow
                };

                _logger.LogInformation("Lip-sync processing completed successfully for request {RequestId}. Quality: {Quality}%, Time: {Time}s", 
                    requestId, result.QualityScore, result.ProcessingTime.TotalSeconds);

                return Ok(response);
            }
            else
            {
                _logger.LogError("Lip-sync processing failed for request {RequestId}: {Error}", 
                    requestId, result.ErrorMessage);

                return BadRequest(new ErrorResponse 
                { 
                    Error = "Processing failed",
                    Details = result.ErrorMessage,
                    RequestId = requestId
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing lip-sync request {RequestId}", requestId);
            return StatusCode(500, new ErrorResponse 
            { 
                Error = "Internal server error",
                Details = "An unexpected error occurred during processing",
                RequestId = requestId
            });
        }
    }

    /// <summary>
    /// Download processed video file
    /// </summary>
    /// <param name="fileName">Name of the output file</param>
    /// <returns>Video file download</returns>
    /// <response code="200">File downloaded successfully</response>
    /// <response code="404">File not found</response>
    [HttpGet("download/{fileName}")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public IActionResult DownloadFile(string fileName)
    {
        try
        {
            var outputDir = _configuration["AiMediaSync:OutputPath"] ?? "Output";
            var filePath = Path.Combine(outputDir, fileName);
            
            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("Download requested for non-existent file: {FileName}", fileName);
                return NotFound(new ErrorResponse 
                { 
                    Error = "File not found",
                    Details = $"The requested file '{fileName}' does not exist or has been removed"
                });
            }

            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            var contentType = GetContentType(fileName);
            
            _logger.LogInformation("File download started: {FileName}, Size: {Size} bytes", fileName, fileBytes.Length);
            
            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file: {FileName}", fileName);
            return StatusCode(500, new ErrorResponse 
            { 
                Error = "Download failed",
                Details = "An error occurred while downloading the file"
            });
        }
    }

    /// <summary>
    /// Get processing status for a job
    /// </summary>
    /// <param name="jobId">Job identifier</param>
    /// <returns>Job status information</returns>
    [HttpGet("status/{jobId}")]
    [ProducesResponseType(typeof(JobStatusResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public async Task<ActionResult<JobStatusResponse>> GetJobStatus(string jobId)
    {
        try
        {
            // In a real implementation, this would query a job queue or database
            // For now, we'll return a placeholder response
            var response = new JobStatusResponse
            {
                JobId = jobId,
                Status = "Completed", // This would be dynamic based on actual job status
                Progress = 100,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job status for {JobId}", jobId);
            return StatusCode(500, new ErrorResponse 
            { 
                Error = "Status check failed",
                Details = "An error occurred while checking job status"
            });
        }
    }

    /// <summary>
    /// Cancel a processing job
    /// </summary>
    /// <param name="jobId">Job identifier</param>
    /// <returns>Cancellation result</returns>
    [HttpDelete("cancel/{jobId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public async Task<IActionResult> CancelJob(string jobId)
    {
        try
        {
            // In a real implementation, this would cancel the job in the queue
            _logger.LogInformation("Job cancellation requested for {JobId}", jobId);
            
            return Ok(new { Message = "Job cancellation requested", JobId = jobId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling job {JobId}", jobId);
            return StatusCode(500, new ErrorResponse 
            { 
                Error = "Cancellation failed",
                Details = "An error occurred while cancelling the job"
            });
        }
    }

    /// <summary>
    /// Get system statistics and health information
    /// </summary>
    /// <returns>System statistics</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(Dictionary<string, object>), 200)]
    public async Task<ActionResult<Dictionary<string, object>>> GetSystemStats()
    {
        try
        {
            var stats = await _syncService.GetSystemStatisticsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system statistics");
            return StatusCode(500, new ErrorResponse 
            { 
                Error = "Statistics unavailable",
                Details = "An error occurred while retrieving system statistics"
            });
        }
    }

    #region Private Helper Methods

    private async Task<string> SaveUploadedFile(IFormFile file, string directory, string prefix)
    {
        var fileName = $"{prefix}_{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(directory, fileName);
        
        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);
        
        return filePath;
    }

    private (bool IsValid, string? ErrorMessage) ValidateRequest(SyncRequest request)
    {
        if (request.VideoFile == null)
            return (false, "Video file is required");
            
        if (request.AudioFile == null)
            return (false, "Audio file is required");

        // Validate video file extension
        var videoExt = Path.GetExtension(request.VideoFile.FileName).ToLower();
        var supportedVideoFormats = _configuration.GetSection("AiMediaSync:SupportedVideoFormats").Get<string[]>() 
            ?? new[] { "mp4", "avi", "mov", "mkv" };
        
        if (!supportedVideoFormats.Contains(videoExt.TrimStart('.')))
            return (false, $"Unsupported video format: {videoExt}");

        // Validate audio file extension
        var audioExt = Path.GetExtension(request.AudioFile.FileName).ToLower();
        var supportedAudioFormats = _configuration.GetSection("AiMediaSync:SupportedAudioFormats").Get<string[]>() 
            ?? new[] { "wav", "mp3", "aac", "flac" };
        
        if (!supportedAudioFormats.Contains(audioExt.TrimStart('.')))
            return (false, $"Unsupported audio format: {audioExt}");

        // Validate quality threshold
        if (request.QualityThreshold < 0 || request.QualityThreshold > 1)
            return (false, "Quality threshold must be between 0.0 and 1.0");

        return (true, null);
    }

    private string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLower();
        return extension switch
        {
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".mkv" => "video/x-matroska",
            _ => "application/octet-stream"
        };
    }

    #endregion
}