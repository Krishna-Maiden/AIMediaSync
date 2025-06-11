using AiMediaSync.Core.Extensions;
using AiMediaSync.Core.Services;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace AiMediaSync.Console;

public class Options
{
    [Option('v', "input-video", Required = true, HelpText = "Path to input video file")]
    public string InputVideo { get; set; } = string.Empty;

    [Option('a', "input-audio", Required = true, HelpText = "Path to input audio file")]
    public string InputAudio { get; set; } = string.Empty;

    [Option('o', "output", Required = true, HelpText = "Path to output video file")]
    public string OutputPath { get; set; } = string.Empty;

    [Option('m', "model", Required = false, HelpText = "Path to ONNX model file")]
    public string? ModelPath { get; set; }

    [Option("verbose", Required = false, HelpText = "Enable verbose logging")]
    public bool Verbose { get; set; } = false;

    [Option("quality", Required = false, HelpText = "Quality threshold (0.0-1.0)", Default = 0.85)]
    public float QualityThreshold { get; set; } = 0.85f;

    [Option("gpu", Required = false, HelpText = "Enable GPU acceleration")]
    public bool EnableGpu { get; set; } = true;
}

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Display banner
        AnsiConsole.Write(
            new FigletText("AiMediaSync")
                .LeftJustified()
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[bold blue]AI-Powered Lip Synchronization Framework[/]");
        AnsiConsole.MarkupLine("[dim]Enterprise Solution for Content Localization[/]");
        AnsiConsole.WriteLine();

        try
        {
            return await Parser.Default.ParseArguments<Options>(args)
                .MapResult(
                    async options => await RunAsync(options),
                    errors => Task.FromResult(1));
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    static async Task<int> RunAsync(Options options)
    {
        // Validate inputs
        if (!File.Exists(options.InputVideo))
        {
            AnsiConsole.MarkupLine($"[red]Error: Input video file not found: {options.InputVideo}[/]");
            return 1;
        }

        if (!File.Exists(options.InputAudio))
        {
            AnsiConsole.MarkupLine($"[red]Error: Input audio file not found: {options.InputAudio}[/]");
            return 1;
        }

        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(options.OutputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Setup dependency injection
        var host = CreateHostBuilder(options).Build();
        
        using var scope = host.Services.CreateScope();
        var aiMediaSyncService = scope.ServiceProvider.GetRequiredService<AiMediaSyncService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        // Display processing information
        var table = new Table()
            .AddColumn("[bold]Parameter[/]")
            .AddColumn("[bold]Value[/]");

        table.AddRow("Input Video", options.InputVideo);
        table.AddRow("Input Audio", options.InputAudio);
        table.AddRow("Output Path", options.OutputPath);
        table.AddRow("Model Path", options.ModelPath ?? "[dim]Not specified[/]");
        table.AddRow("Quality Threshold", $"{options.QualityThreshold:F2}");
        table.AddRow("GPU Acceleration", options.EnableGpu ? "[green]Enabled[/]" : "[red]Disabled[/]");
        table.AddRow("Verbose Logging", options.Verbose ? "[green]Enabled[/]" : "[red]Disabled[/]");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Process with progress bar
        var result = await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Processing lip-sync[/]");
                task.MaxValue = 100;

                // Start processing
                var processingTask = aiMediaSyncService.ProcessLipSyncAsync(
                    options.InputVideo,
                    options.InputAudio,
                    options.OutputPath,
                    options.ModelPath);

                // Simulate progress updates (in a real implementation, this would be actual progress)
                var progressUpdateTask = Task.Run(async () =>
                {
                    while (!processingTask.IsCompleted)
                    {
                        await Task.Delay(500);
                        if (task.Value < 95)
                        {
                            task.Increment(2);
                        }
                    }
                });

                var syncResult = await processingTask;
                await progressUpdateTask;
                
                task.Value = 100;
                return syncResult;
            });

        // Display results
        AnsiConsole.WriteLine();
        
        if (result.IsSuccess)
        {
            AnsiConsole.MarkupLine("[bold green]✓ Processing completed successfully![/]");
            
            var resultsTable = new Table()
                .AddColumn("[bold]Metric[/]")
                .AddColumn("[bold]Value[/]");

            resultsTable.AddRow("Output File", result.OutputPath);
            resultsTable.AddRow("Processing Time", $"{result.ProcessingTime.TotalSeconds:F2} seconds");
            resultsTable.AddRow("Quality Score", $"{result.QualityScore:F1}%");
            
            if (result.Metrics != null)
            {
                resultsTable.AddRow("Frames Processed", $"{result.Metrics.ProcessedFrames}/{result.Metrics.TotalFrames}");
                resultsTable.AddRow("Average Confidence", $"{result.Metrics.AverageConfidence:F3}");
                resultsTable.AddRow("Audio-Video Alignment", $"{result.Metrics.AudioVideoAlignment:F3}");
                resultsTable.AddRow("Avg Frame Time", $"{result.Metrics.AverageFrameProcessingTime.TotalMilliseconds:F1}ms");
            }

            AnsiConsole.Write(resultsTable);
            
            // Performance summary
            if (result.Metrics != null && result.ProcessingTime.TotalSeconds > 0)
            {
                var framesPerSecond = result.Metrics.TotalFrames / result.ProcessingTime.TotalSeconds;
                var speedMultiplier = framesPerSecond / 25.0; // Assuming 25 FPS video
                
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[bold cyan]Performance:[/] {framesPerSecond:F1} FPS ({speedMultiplier:F1}x real-time)");
            }
            
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine("[bold red]✗ Processing failed![/]");
            AnsiConsole.MarkupLine($"[red]Error: {result.ErrorMessage}[/]");
            
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                logger.LogError("Processing failed: {ErrorMessage}", result.ErrorMessage);
            }
            
            return 1;
        }
    }

    static IHostBuilder CreateHostBuilder(Options options) =>
        Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Add AiMediaSync services with configuration
                services.AddAiMediaSyncServices(opts =>
                {
                    opts.EnableGpuAcceleration = options.EnableGpu;
                    opts.DefaultQualityThreshold = options.QualityThreshold;
                });
                
                // Configure logging level based on verbose flag
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddConsole();
                    
                    if (options.Verbose)
                    {
                        builder.SetMinimumLevel(LogLevel.Debug);
                    }
                    else
                    {
                        builder.SetMinimumLevel(LogLevel.Information);
                    }
                });
            });
}