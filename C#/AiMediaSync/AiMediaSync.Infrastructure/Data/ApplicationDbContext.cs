// AiMediaSync.Infrastructure/Data/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using AiMediaSync.Infrastructure.Data.Entities;

namespace AiMediaSync.Infrastructure.Data;

/// <summary>
/// Main database context for AiMediaSync application
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    // Job tracking
    public DbSet<ProcessingJob> ProcessingJobs { get; set; } = null!;
    public DbSet<JobMetrics> JobMetrics { get; set; } = null!;
    
    // Model management
    public DbSet<AiModel> AiModels { get; set; } = null!;
    public DbSet<ModelVersion> ModelVersions { get; set; } = null!;
    
    // User and authentication
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<ApiKey> ApiKeys { get; set; } = null!;
    
    // Audit and logging
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<ProcessingMetrics> ProcessingMetrics { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure entities
        ConfigureProcessingJob(modelBuilder);
        ConfigureJobMetrics(modelBuilder);
        ConfigureAiModel(modelBuilder);
        ConfigureUser(modelBuilder);
        ConfigureAuditLog(modelBuilder);
        ConfigureProcessingMetrics(modelBuilder);

        // Add indexes for performance
        AddIndexes(modelBuilder);
    }

    private void ConfigureProcessingJob(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessingJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            
            entity.Property(e => e.JobId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Priority).IsRequired();
            
            entity.Property(e => e.InputVideoPath).HasMaxLength(500);
            entity.Property(e => e.InputAudioPath).HasMaxLength(500);
            entity.Property(e => e.OutputVideoPath).HasMaxLength(500);
            
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            
            entity.HasIndex(e => e.JobId).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });
    }

    private void ConfigureJobMetrics(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobMetrics>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            
            entity.HasOne<ProcessingJob>()
                .WithMany()
                .HasForeignKey(e => e.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private void ConfigureAiModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AiModel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ModelType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(1000);
            
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.ModelType);
        });
    }

    private void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(200);
            entity.Property(e => e.FirstName).HasMaxLength(50);
            entity.Property(e => e.LastName).HasMaxLength(50);
            
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });
    }

    private void ConfigureAuditLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EntityType).HasMaxLength(100);
            entity.Property(e => e.EntityId).HasMaxLength(50);
            entity.Property(e => e.UserId).HasMaxLength(50);
            entity.Property(e => e.Changes).HasColumnType("nvarchar(max)");
            
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.UserId);
        });
    }

    private void ConfigureProcessingMetrics(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessingMetrics>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            
            entity.Property(e => e.MetricName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.MetricValue).IsRequired();
            entity.Property(e => e.Unit).HasMaxLength(20);
            entity.Property(e => e.Category).HasMaxLength(50);
            
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.MetricName);
        });
    }

    private void AddIndexes(ModelBuilder modelBuilder)
    {
        // Composite indexes for common queries
        modelBuilder.Entity<ProcessingJob>()
            .HasIndex(e => new { e.Status, e.CreatedAt });
            
        modelBuilder.Entity<ProcessingJob>()
            .HasIndex(e => new { e.CreatedBy, e.Status });
            
        modelBuilder.Entity<ProcessingMetrics>()
            .HasIndex(e => new { e.MetricName, e.Timestamp });
    }
}

// AiMediaSync.Infrastructure/Data/Entities/ProcessingJob.cs
namespace AiMediaSync.Infrastructure.Data.Entities;

public class ProcessingJob
{
    public int Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string? InputVideoPath { get; set; }
    public string? InputAudioPath { get; set; }
    public string? OutputVideoPath { get; set; }
    public string? ModelPath { get; set; }
    public float QualityThreshold { get; set; }
    public bool EnableGpuAcceleration { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? ProcessingTime { get; set; }
    public float? QualityScore { get; set; }
    public string? ErrorMessage { get; set; }
    public string? CreatedBy { get; set; }
    public string? WebhookUrl { get; set; }
    public string? Metadata { get; set; }
}

public class JobMetrics
{
    public int Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public int TotalFrames { get; set; }
    public int ProcessedFrames { get; set; }
    public float AverageConfidence { get; set; }
    public float AudioVideoAlignment { get; set; }
    public TimeSpan AverageFrameProcessingTime { get; set; }
    public long MemoryUsage { get; set; }
    public double CpuUsage { get; set; }
    public DateTime RecordedAt { get; set; }
}

public class AiModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ModelType { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Version { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string CheckSum { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsed { get; set; }
    public bool IsActive { get; set; }
    public string? Configuration { get; set; }
}

public class ModelVersion
{
    public int Id { get; set; }
    public int ModelId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? ReleaseNotes { get; set; }
    public DateTime ReleasedAt { get; set; }
    public bool IsLatest { get; set; }
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? Preferences { get; set; }
}

public class ApiKey
{
    public int Id { get; set; }
    public string KeyHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsActive { get; set; }
    public string? Permissions { get; set; }
}

public class AuditLog
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? UserId { get; set; }
    public string? Changes { get; set; }
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public class ProcessingMetrics
{
    public int Id { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public double MetricValue { get; set; }
    public string? Unit { get; set; }
    public string? Category { get; set; }
    public DateTime Timestamp { get; set; }
    public string? JobId { get; set; }
    public string? AdditionalData { get; set; }
}