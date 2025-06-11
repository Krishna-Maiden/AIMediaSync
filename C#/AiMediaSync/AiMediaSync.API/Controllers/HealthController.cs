// HealthController.cs
using Microsoft.AspNetCore.Mvc;
using AiMediaSync.Core.Services;
using AiMediaSync.API.Services;

namespace AiMediaSync.API.Controllers;

/// <summary>
/// Health check and system status controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly AiMediaSyncService _syncService;
    private readonly JobService _jobService;
    private readonly FileService _fileService;

    public HealthController(
        ILogger<HealthController> logger,
        AiMediaSyncService syncService,
        JobService jobService,
        FileService fileService)
    {
        _logger = logger;
        _syncService = syncService;
        _jobService = jobService;
        _fileService = fileService;
    }

    /// <summary>
    /// Basic health check
    /// </summary>
    /// <returns>Health status</returns>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), 200)]
    public async Task<ActionResult<HealthResponse>> GetHealth()
    {
        var response = new HealthResponse
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
        };

        return Ok(response);
    }

    /// <summary>
    /// Detailed health check with dependencies
    /// </summary>
    /// <returns>Detailed health status</returns>
    [HttpGet("detailed")]
    [ProducesResponseType(typeof(DetailedHealthResponse), 200)]
    public async Task<ActionResult<DetailedHealthResponse>> GetDetailedHealth()
    {
        var response = new DetailedHealthResponse
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
            Checks = new Dictionary<string, HealthCheckResult>()
        };

        // Check AI service
        response.Checks["AiService"] = await CheckAiServiceAsync();
        
        // Check job service
        response.Checks["JobService"] = CheckJobService();
        
        // Check file system
        response.Checks["FileSystem"] = await CheckFileSystemAsync();
        
        // Check memory usage
        response.Checks["Memory"] = CheckMemoryUsage();

        // Determine overall status
        var hasFailures = response.Checks.Values.Any(c => c.Status != "Healthy");
        response.Status = hasFailures ? "Unhealthy" : "Healthy";

        return Ok(response);
    }

    /// <summary>
    /// Get system statistics
    /// </summary>
    /// <returns>System statistics</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(SystemStats), 200)]
    public async Task<ActionResult<SystemStats>> GetStats()
    {
        var systemStats = await _syncService.GetSystemStatisticsAsync();
        var storageStats = await _fileService.GetStorageStatsAsync();
        var jobStats = GetJobStatistics();

        var stats = new SystemStats
        {
            System = systemStats,
            Storage = storageStats,
            Jobs = jobStats,
            Timestamp = DateTime.UtcNow
        };

        return Ok(stats);
    }

    /// <summary>
    /// Get job queue status
    /// </summary>
    /// <returns>Job queue information</returns>
    [HttpGet("jobs")]
    [ProducesResponseType(typeof(JobQueueStatus), 200)]
    public ActionResult<JobQueueStatus> GetJobStatus()
    {
        var activeJobs = _jobService.GetActiveJobs().ToList();
        var queuedJobs = _jobService.GetQueuedJobs().ToList();

        var status = new JobQueueStatus
        {
            ActiveJobs = activeJobs.Count,
            QueuedJobs = queuedJobs.Count,
            ActiveJobDetails = activeJobs.Select(j => new JobSummary
            {
                JobId = j.JobId,
                Status = j.Status.ToString(),
                StartedAt = j.StartedAt,
                Priority = j.Priority.ToString()
            }).ToList(),
            QueuedJobDetails = queuedJobs.Take(10).Select(j => new JobSummary
            {
                JobId = j.JobId,
                Status = j.Status.ToString(),
                CreatedAt = j.CreatedAt,
                Priority = j.Priority.ToString()
            }).ToList(),
            Timestamp = DateTime.UtcNow
        };

        return Ok(status);
    }

    private async Task<HealthCheckResult> CheckAiServiceAsync()
    {
        try
        {
            var stats = await _syncService.GetSystemStatisticsAsync();
            var modelLoaded = stats.ContainsKey("ModelLoaded") && (bool)stats["ModelLoaded"];
            
            return new HealthCheckResult
            {
                Status = modelLoaded ? "Healthy" : "Degraded",
                Description = modelLoaded ? "AI models loaded and ready" : "AI models not loaded",
                Data = new Dictionary<string, object>
                {
                    ["ModelLoaded"] = modelLoaded,
                    ["ProcessorCount"] = stats.GetValueOrDefault("ProcessorCount", 0),
                    ["MemoryUsage"] = stats.GetValueOrDefault("MemoryUsage", 0L)
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Status = "Unhealthy",
                Description = "AI service check failed",
                Exception = ex.Message
            };
        }
    }

    private HealthCheckResult CheckJobService()
    {
        try
        {
            var activeJobs = _jobService.GetActiveJobs().Count();
            var queuedJobs = _jobService.GetQueuedJobs().Count();
            
            return new HealthCheckResult
            {
                Status = "Healthy",
                Description = "Job service operational",
                Data = new Dictionary<string, object>
                {
                    ["ActiveJobs"] = activeJobs,
                    ["QueuedJobs"] = queuedJobs
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Status = "Unhealthy",
                Description = "Job service check failed",
                Exception = ex.Message
            };
        }
    }

    private async Task<HealthCheckResult> CheckFileSystemAsync()
    {
        try
        {
            var storageStats = await _fileService.GetStorageStatsAsync();
            
            // Check if we can write to temp and output directories
            var tempTestFile = Path.Combine("Temp", $"health_check_{Guid.NewGuid()}.tmp");
            var outputTestFile = Path.Combine("Output", $"health_check_{Guid.NewGuid()}.tmp");
            
            await File.WriteAllTextAsync(tempTestFile, "health check");
            await File.WriteAllTextAsync(outputTestFile, "health check");
            
            File.Delete(tempTestFile);
            File.Delete(outputTestFile);
            
            return new HealthCheckResult
            {
                Status = "Healthy",
                Description = "File system accessible",
                Data = new Dictionary<string, object>
                {
                    ["TempFiles"] = storageStats.TempFileCount,
                    ["OutputFiles"] = storageStats.OutputFileCount,
                    ["TotalStorage"] = storageStats.TotalStorageFormatted
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Status = "Unhealthy",
                Description = "File system check failed",
                Exception = ex.Message
            };
        }
    }

    private HealthCheckResult CheckMemoryUsage()
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var workingSet = process.WorkingSet64;
            var privateMemory = process.PrivateMemorySize64;
            
            // Consider unhealthy if using more than 4GB
            var isHealthy = workingSet < 4L * 1024 * 1024 * 1024;
            
            return new HealthCheckResult
            {
                Status = isHealthy ? "Healthy" : "Degraded",
                Description = isHealthy ? "Memory usage normal" : "High memory usage",
                Data = new Dictionary<string, object>
                {
                    ["WorkingSetMB"] = workingSet / (1024 * 1024),
                    ["PrivateMemoryMB"] = privateMemory / (1024 * 1024),
                    ["GCTotalMemoryMB"] = GC.GetTotalMemory(false) / (1024 * 1024)
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Status = "Unhealthy",
                Description = "Memory check failed",
                Exception = ex.Message
            };
        }
    }

    private JobStatistics GetJobStatistics()
    {
        var activeJobs = _jobService.GetActiveJobs().ToList();
        var queuedJobs = _jobService.GetQueuedJobs().ToList();
        
        return new JobStatistics
        {
            ActiveJobs = activeJobs.Count,
            QueuedJobs = queuedJobs.Count,
            TotalJobs = activeJobs.Count + queuedJobs.Count,
            AverageProcessingTime = TimeSpan.Zero, // Would need to track this
            JobsCompletedToday = 0, // Would need to track this
            JobsFailedToday = 0 // Would need to track this
        };
    }
}

// Health response models
public class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
}

public class DetailedHealthResponse : HealthResponse
{
    public Dictionary<string, HealthCheckResult> Checks { get; set; } = new();
}

public class HealthCheckResult
{
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object>? Data { get; set; }
    public string? Exception { get; set; }
}

public class SystemStats
{
    public Dictionary<string, object> System { get; set; } = new();
    public StorageStats Storage { get; set; } = new();
    public JobStatistics Jobs { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class JobStatistics
{
    public int ActiveJobs { get; set; }
    public int QueuedJobs { get; set; }
    public int TotalJobs { get; set; }
    public TimeSpan AverageProcessingTime { get; set; }
    public int JobsCompletedToday { get; set; }
    public int JobsFailedToday { get; set; }
}

public class JobQueueStatus
{
    public int ActiveJobs { get; set; }
    public int QueuedJobs { get; set; }
    public List<JobSummary> ActiveJobDetails { get; set; } = new();
    public List<JobSummary> QueuedJobDetails { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class JobSummary
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
}