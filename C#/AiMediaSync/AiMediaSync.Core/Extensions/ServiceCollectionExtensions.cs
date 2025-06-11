using AiMediaSync.Core.Interfaces;
using AiMediaSync.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiMediaSync.Core.Extensions;

/// <summary>
/// Extension methods for configuring AiMediaSync services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add all AiMediaSync core services to the dependency injection container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAiMediaSyncServices(this IServiceCollection services)
    {
        // Core services
        services.AddScoped<IAudioProcessor, AudioProcessor>();
        services.AddScoped<IFaceProcessor, FaceProcessor>();
        services.AddScoped<IVideoProcessor, VideoProcessor>();
        services.AddScoped<ILipSyncModel, LipSyncModel>();
        services.AddScoped<IGuidanceSystem, GuidanceSystem>();
        
        // Main orchestrator service
        services.AddScoped<AiMediaSyncService>();
        
        // Logging configuration
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
        });

        return services;
    }

    /// <summary>
    /// Add AiMediaSync services with custom configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOptions">Configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAiMediaSyncServices(this IServiceCollection services, 
        Action<AiMediaSyncOptions> configureOptions)
    {
        services.Configure(configureOptions);
        return services.AddAiMediaSyncServices();
    }
}

/// <summary>
/// Configuration options for AiMediaSync services
/// </summary>
public class AiMediaSyncOptions
{
    /// <summary>
    /// Path to AI models directory
    /// </summary>
    public string ModelsPath { get; set; } = "Models";
    
    /// <summary>
    /// Enable GPU acceleration if available
    /// </summary>
    public bool EnableGpuAcceleration { get; set; } = true;
    
    /// <summary>
    /// Maximum number of concurrent processing jobs
    /// </summary>
    public int MaxConcurrentJobs { get; set; } = 4;
    
    /// <summary>
    /// Default quality threshold for processing
    /// </summary>
    public float DefaultQualityThreshold { get; set; } = 0.85f;
    
    /// <summary>
    /// Processing timeout duration
    /// </summary>
    public TimeSpan ProcessingTimeout { get; set; } = TimeSpan.FromMinutes(30);
    
    /// <summary>
    /// Temporary files directory
    /// </summary>
    public string TempPath { get; set; } = "Temp";
    
    /// <summary>
    /// Output files directory
    /// </summary>
    public string OutputPath { get; set; } = "Output";
}