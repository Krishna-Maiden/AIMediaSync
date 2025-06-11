using CommandLine;

namespace AiMediaSync.Console;

/// <summary>
/// Command-line options for AiMediaSync console application
/// </summary>
public class Options
{
    [Option('v', "input-video", Required = true, HelpText = "Path to input video file (MP4, AVI, MOV, MKV)")]
    public string InputVideo { get; set; } = string.Empty;

    [Option('a', "input-audio", Required = true, HelpText = "Path to input audio file (WAV, MP3, AAC, FLAC)")]
    public string InputAudio { get; set; } = string.Empty;

    [Option('o', "output", Required = true, HelpText = "Path to output video file")]
    public string OutputPath { get; set; } = string.Empty;

    [Option('m', "model", Required = false, HelpText = "Path to custom ONNX model file for lip-sync")]
    public string? ModelPath { get; set; }

    [Option("models-dir", Required = false, HelpText = "Directory containing AI models", Default = "Models")]
    public string ModelsDirectory { get; set; } = "Models";

    [Option("verbose", Required = false, HelpText = "Enable verbose logging output")]
    public bool Verbose { get; set; } = false;

    [Option("debug", Required = false, HelpText = "Enable debug-level logging")]
    public bool Debug { get; set; } = false;

    [Option("quality", Required = false, HelpText = "Quality threshold (0.0-1.0)", Default = 0.85)]
    public float QualityThreshold { get; set; } = 0.85f;

    [Option("gpu", Required = false, HelpText = "Enable GPU acceleration (requires CUDA)", Default = true)]
    public bool EnableGpu { get; set; } = true;

    [Option("timeout", Required = false, HelpText = "Processing timeout in minutes", Default = 30)]
    public int TimeoutMinutes { get; set; } = 30;

    [Option("temp-dir", Required = false, HelpText = "Temporary files directory", Default = "Temp")]
    public string TempDirectory { get; set; } = "Temp";

    [Option("threads", Required = false, HelpText = "Number of processing threads (0 = auto)", Default = 0)]
    public int ThreadCount { get; set; } = 0;

    [Option("sample-rate", Required = false, HelpText = "Audio sample rate for processing", Default = 16000)]
    public int SampleRate { get; set; } = 16000;

    [Option("face-threshold", Required = false, HelpText = "Face detection confidence threshold", Default = 0.5)]
    public float FaceThreshold { get; set; } = 0.5f;

    [Option("batch-size", Required = false, HelpText = "Processing batch size", Default = 1)]
    public int BatchSize { get; set; } = 1;

    [Option("preserve-audio", Required = false, HelpText = "Preserve original audio track")]
    public bool PreserveAudio { get; set; } = false;

    [Option("output-format", Required = false, HelpText = "Output video format (mp4, avi, mov)", Default = "mp4")]
    public string OutputFormat { get; set; } = "mp4";

    [Option("resolution", Required = false, HelpText = "Output resolution (480p, 720p, 1080p, 4k)", Default = "original")]
    public string Resolution { get; set; } = "original";

    [Option("fps", Required = false, HelpText = "Output frame rate (0 = preserve original)", Default = 0)]
    public double FrameRate { get; set; } = 0;

    [Option("preview", Required = false, HelpText = "Generate preview (first 30 seconds only)")]
    public bool PreviewMode { get; set; } = false;

    [Option("validate-only", Required = false, HelpText = "Only validate inputs without processing")]
    public bool ValidateOnly { get; set; } = false;

    [Option("benchmark", Required = false, HelpText = "Run performance benchmark")]
    public bool Benchmark { get; set; } = false;

    [Option("stats", Required = false, HelpText = "Show detailed processing statistics")]
    public bool ShowStats { get; set; } = false;

    [Option("config", Required = false, HelpText = "Path to configuration file")]
    public string? ConfigFile { get; set; }

    [Option("log-file", Required = false, HelpText = "Path to log file")]
    public string? LogFile { get; set; }

    [Option("no-cleanup", Required = false, HelpText = "Don't cleanup temporary files")]
    public bool NoCleanup { get; set; } = false;

    [Option("force", Required = false, HelpText = "Force overwrite existing output file")]
    public bool Force { get; set; } = false;

    [Option("quiet", Required = false, HelpText = "Suppress non-error output")]
    public bool Quiet { get; set; } = false;

    [Option("progress", Required = false, HelpText = "Show detailed progress information")]
    public bool ShowProgress { get; set; } = true;

    [Option("webhook", Required = false, HelpText = "Webhook URL for completion notification")]
    public string? WebhookUrl { get; set; }

    [Option("metadata", Required = false, HelpText = "Include processing metadata in output")]
    public bool IncludeMetadata { get; set; } = false;

    [Option("experimental", Required = false, HelpText = "Enable experimental features")]
    public bool ExperimentalFeatures { get; set; } = false;
}

/// <summary>
/// Batch processing options
/// </summary>
[Verb("batch", HelpText = "Process multiple video files in batch")]
public class BatchOptions
{
    [Option('i', "input-list", Required = true, HelpText = "Path to input list file (CSV or JSON)")]
    public string InputList { get; set; } = string.Empty;

    [Option('o', "output-dir", Required = true, HelpText = "Output directory for processed videos")]
    public string OutputDirectory { get; set; } = string.Empty;

    [Option("parallel", Required = false, HelpText = "Number of parallel processing jobs", Default = 2)]
    public int ParallelJobs { get; set; } = 2;

    [Option("continue-on-error", Required = false, HelpText = "Continue processing if one file fails")]
    public bool ContinueOnError { get; set; } = false;

    [Option("report", Required = false, HelpText = "Generate processing report")]
    public bool GenerateReport { get; set; } = true;

    [Option("dry-run", Required = false, HelpText = "Validate inputs without processing")]
    public bool DryRun { get; set; } = false;

    [Value(0, MetaName = "files", HelpText = "Additional input files")]
    public IEnumerable<string> InputFiles { get; set; } = new List<string>();
}

/// <summary>
/// Model management options
/// </summary>
[Verb("model", HelpText = "Manage AI models")]
public class ModelOptions
{
    [Option("download", Required = false, HelpText = "Download required models")]
    public bool Download { get; set; } = false;

    [Option("validate", Required = false, HelpText = "Validate installed models")]
    public bool Validate { get; set; } = false;

    [Option("list", Required = false, HelpText = "List available models")]
    public bool List { get; set; } = false;

    [Option("update", Required = false, HelpText = "Update models to latest version")]
    public bool Update { get; set; } = false;

    [Option("path", Required = false, HelpText = "Models directory path", Default = "Models")]
    public string ModelsPath { get; set; } = "Models";

    [Option("model-name", Required = false, HelpText = "Specific model name to manage")]
    public string? ModelName { get; set; }

    [Option("force", Required = false, HelpText = "Force download/update even if model exists")]
    public bool Force { get; set; } = false;
}

/// <summary>
/// Configuration management options
/// </summary>
[Verb("config", HelpText = "Manage configuration")]
public class ConfigOptions
{
    [Option("init", Required = false, HelpText = "Initialize default configuration")]
    public bool Initialize { get; set; } = false;

    [Option("validate", Required = false, HelpText = "Validate configuration")]
    public bool Validate { get; set; } = false;

    [Option("show", Required = false, HelpText = "Show current configuration")]
    public bool Show { get; set; } = false;

    [Option("reset", Required = false, HelpText = "Reset to default configuration")]
    public bool Reset { get; set; } = false;

    [Option("export", Required = false, HelpText = "Export configuration to file")]
    public string? ExportPath { get; set; }

    [Option("import", Required = false, HelpText = "Import configuration from file")]
    public string? ImportPath { get; set; }

    [Option("set", Required = false, HelpText = "Set configuration value (key=value)")]
    public IEnumerable<string> SetValues { get; set; } = new List<string>();
}

/// <summary>
/// System information options
/// </summary>
[Verb("info", HelpText = "Show system information")]
public class InfoOptions
{
    [Option("system", Required = false, HelpText = "Show system information")]
    public bool System { get; set; } = true;

    [Option("models", Required = false, HelpText = "Show model information")]
    public bool Models { get; set; } = false;

    [Option("performance", Required = false, HelpText = "Show performance information")]
    public bool Performance { get; set; } = false;

    [Option("gpu", Required = false, HelpText = "Show GPU information")]
    public bool Gpu { get; set; } = false;

    [Option("formats", Required = false, HelpText = "Show supported formats")]
    public bool Formats { get; set; } = false;

    [Option("version", Required = false, HelpText = "Show version information")]
    public bool Version { get; set; } = false;

    [Option("json", Required = false, HelpText = "Output in JSON format")]
    public bool JsonOutput { get; set; } = false;
}

/// <summary>
/// Validation helper for command-line options
/// </summary>
public static class OptionsValidator
{
    public static (bool IsValid, string? ErrorMessage) ValidateOptions(Options options)
    {
        // Validate input files exist
        if (!File.Exists(options.InputVideo))
            return (false, $"Input video file not found: {options.InputVideo}");

        if (!File.Exists(options.InputAudio))
            return (false, $"Input audio file not found: {options.InputAudio}");

        // Validate file extensions
        var videoExt = Path.GetExtension(options.InputVideo).ToLower();
        var supportedVideoFormats = new[] { ".mp4", ".avi", ".mov", ".mkv" };
        if (!supportedVideoFormats.Contains(videoExt))
            return (false, $"Unsupported video format: {videoExt}");

        var audioExt = Path.GetExtension(options.InputAudio).ToLower();
        var supportedAudioFormats = new[] { ".wav", ".mp3", ".aac", ".flac" };
        if (!supportedAudioFormats.Contains(audioExt))
            return (false, $"Unsupported audio format: {audioExt}");

        // Validate quality threshold
        if (options.QualityThreshold < 0 || options.QualityThreshold > 1)
            return (false, "Quality threshold must be between 0.0 and 1.0");

        // Validate face threshold
        if (options.FaceThreshold < 0 || options.FaceThreshold > 1)
            return (false, "Face threshold must be between 0.0 and 1.0");

        // Validate timeout
        if (options.TimeoutMinutes < 1 || options.TimeoutMinutes > 240)
            return (false, "Timeout must be between 1 and 240 minutes");

        // Validate sample rate
        var validSampleRates = new[] { 8000, 16000, 22050, 44100, 48000 };
        if (!validSampleRates.Contains(options.SampleRate))
            return (false, "Sample rate must be one of: 8000, 16000, 22050, 44100, 48000");

        // Validate output directory
        var outputDir = Path.GetDirectoryName(options.OutputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            try
            {
                Directory.CreateDirectory(outputDir);
            }
            catch (Exception ex)
            {
                return (false, $"Cannot create output directory: {ex.Message}");
            }
        }

        // Check if output file exists and force flag
        if (File.Exists(options.OutputPath) && !options.Force)
            return (false, $"Output file already exists: {options.OutputPath}. Use --force to overwrite.");

        // Validate custom model path if provided
        if (!string.IsNullOrEmpty(options.ModelPath) && !File.Exists(options.ModelPath))
            return (false, $"Custom model file not found: {options.ModelPath}");

        return (true, null);
    }

    public static Dictionary<string, object> GetProcessingConfiguration(Options options)
    {
        return new Dictionary<string, object>
        {
            ["QualityThreshold"] = options.QualityThreshold,
            ["EnableGpu"] = options.EnableGpu,
            ["SampleRate"] = options.SampleRate,
            ["FaceThreshold"] = options.FaceThreshold,
            ["BatchSize"] = options.BatchSize,
            ["ThreadCount"] = options.ThreadCount > 0 ? options.ThreadCount : Environment.ProcessorCount,
            ["TimeoutMinutes"] = options.TimeoutMinutes,
            ["PreserveAudio"] = options.PreserveAudio,
            ["OutputFormat"] = options.OutputFormat,
            ["Resolution"] = options.Resolution,
            ["FrameRate"] = options.FrameRate,
            ["PreviewMode"] = options.PreviewMode,
            ["ExperimentalFeatures"] = options.ExperimentalFeatures
        };
    }
}