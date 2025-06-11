// JobService.cs
using AiMediaSync.Core.Services;
using AiMediaSync.API.Models;
using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;

namespace AiMediaSync.API.Services;

/// <summary>
/// Background job processing service
/// </summary>
public class JobService : BackgroundService
{
    private readonly ILogger<JobService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, ProcessingJob> _jobs = new();
    private readonly SemaphoreSlim _processingSlots;
    private readonly int _maxConcurrentJobs;

    public JobService(
        ILogger<JobService> logger, 
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _maxConcurrentJobs = configuration.GetValue<int>("AiMediaSync:MaxConcurrentJobs", 4);
        _processingSlots = new SemaphoreSlim(_maxConcurrentJobs, _maxConcurrentJobs);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job service started with {MaxJobs} concurrent slots", _maxConcurrentJobs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
                await Task.Delay(1000, stoppingToken); // Check every second
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in job processing loop");
                await Task.Delay(5000, stoppingToken); // Wait before retrying
            }
        }

        _logger.LogInformation("Job service stopped");
    }

    public string EnqueueJob(SyncRequest request, string requestId)
    {
        var job = new ProcessingJob
        {
            JobId = requestId,
            Request = request,
            Status = JobStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            Priority = request.Priority
        };

        _jobs.TryAdd(requestId, job);
        _logger.LogInformation("Job {JobId} enqueued with priority {Priority}", requestId, request.Priority);
        
        return requestId;
    }

    public ProcessingJob? GetJob(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return job;
    }

    public bool CancelJob(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            if (job.Status == JobStatus.Queued)
            {
                job.Status = JobStatus.Cancelled;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("Job {JobId} cancelled", jobId);
                return true;
            }
            else if (job.Status == JobStatus.Processing)
            {
                job.CancellationTokenSource?.Cancel();
                _logger.LogInformation("Cancellation requested for job {JobId}", jobId);
                return true;
            }
        }
        return false;
    }

    public IEnumerable<ProcessingJob> GetActiveJobs()
    {
        return _jobs.Values.Where(j => j.Status == JobStatus.Processing);
    }

    public IEnumerable<ProcessingJob> GetQueuedJobs()
    {
        return _jobs.Values
            .Where(j => j.Status == JobStatus.Queued)
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt);
    }

    private async Task ProcessPendingJobsAsync(CancellationToken stoppingToken)
    {
        var queuedJobs = GetQueuedJobs().ToList();
        
        foreach (var job in queuedJobs)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            if (await _processingSlots.WaitAsync(0, stoppingToken))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessJobAsync(job, stoppingToken);
                    }
                    finally
                    {
                        _processingSlots.Release();
                    }
                }, stoppingToken);
            }
            else
            {
                break; // No available slots
            }
        }
    }

    private async Task ProcessJobAsync(ProcessingJob job, CancellationToken stoppingToken)
    {
        job.Status = JobStatus.Processing;
        job.StartedAt = DateTime.UtcNow;
        job.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        _logger.LogInformation("Starting job processing: {JobId}", job.JobId);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<AiMediaSyncService>();

            // Save uploaded files to temporary location
            var tempDir = Path.Combine("Temp", job.JobId);
            Directory.CreateDirectory(tempDir);

            var videoPath = await SaveUploadedFile(job.Request.VideoFile, tempDir, "video");
            var audioPath = await SaveUploadedFile(job.Request.AudioFile, tempDir, "audio");
            
            var outputDir = "Output";
            Directory.CreateDirectory(outputDir);
            var outputPath = Path.Combine(outputDir, $"synced_{job.JobId}.mp4");

            // Process the lip-sync
            var result = await syncService.ProcessLipSyncAsync(
                videoPath, 
                audioPath, 
                outputPath, 
                job.Request.ModelPath);

            job.Result = result;
            job.Status = result.IsSuccess ? JobStatus.Completed : JobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;

            if (result.IsSuccess)
            {
                _logger.LogInformation("Job {JobId} completed successfully in {Duration}s", 
                    job.JobId, result.ProcessingTime.TotalSeconds);
            }
            else
            {
                _logger.LogError("Job {JobId} failed: {Error}", job.JobId, result.ErrorMessage);
            }

            // Send webhook notification if configured
            if (!string.IsNullOrEmpty(job.Request.WebhookUrl))
            {
                await SendWebhookNotificationAsync(job);
            }

            // Cleanup temporary files after delay
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromHours(1)); // Keep temp files for 1 hour
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup temp directory: {TempDir}", tempDir);
                }
            });
        }
        catch (OperationCanceledException)
        {
            job.Status = JobStatus.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Job {JobId} was cancelled", job.JobId);
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            job.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Job {JobId} failed with exception", job.JobId);
        }
    }

    private async Task<string> SaveUploadedFile(IFormFile file, string directory, string prefix)
    {
        var fileName = $"{prefix}_{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(directory, fileName);
        
        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);
        
        return filePath;
    }

    private async Task SendWebhookNotificationAsync(ProcessingJob job)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var payload = new
            {
                jobId = job.JobId,
                status = job.Status.ToString(),
                completedAt = job.CompletedAt,
                processingTime = job.Result?.ProcessingTime,
                qualityScore = job.Result?.QualityScore,
                outputUrl = job.Result?.IsSuccess == true ? $"/api/sync/download/{Path.GetFileName(job.Result.OutputPath)}" : null,
                errorMessage = job.ErrorMessage
            };

            var jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(job.Request.WebhookUrl, content);
            
            _logger.LogInformation("Webhook notification sent for job {JobId}: {StatusCode}", 
                job.JobId, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send webhook notification for job {JobId}", job.JobId);
        }