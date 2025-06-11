using AiMediaSync.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Collections.Concurrent;

namespace AiMediaSync.Core.Services;

/// <summary>
/// Lip-sync model implementation using ONNX Runtime
/// </summary>
public class LipSyncModel : ILipSyncModel, IDisposable
{
    private readonly ILogger<LipSyncModel> _logger;
    private InferenceSession? _session;
    private readonly ConcurrentDictionary<string, InferenceSession> _modelCache = new();
    private readonly SemaphoreSlim _loadingSemaphore = new(1, 1);
    private bool _disposed = false;

    public LipSyncModel(ILogger<LipSyncModel> logger)
    {
        _logger = logger;
    }

    public async Task<float[]> PredictAsync(float[] audioFeatures, float[] visualFeatures)
    {
        if (_session == null)
        {
            throw new InvalidOperationException("Model not loaded. Call LoadModelAsync first.");
        }

        try
        {
            // Prepare input tensors
            var audioTensor = new DenseTensor<float>(audioFeatures, new[] { 1, audioFeatures.Length });
            var visualTensor = new DenseTensor<float>(visualFeatures, new[] { 1, visualFeatures.Length });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("audio_features", audioTensor),
                NamedOnnxValue.CreateFromTensor("visual_features", visualTensor)
            };

            // Run inference
            using var results = _session.Run(inputs);
            var output = results.FirstOrDefault()?.AsTensor<float>();

            if (output == null)
            {
                throw new InvalidOperationException("Model inference failed - no output received");
            }

            return output.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during lip-sync prediction");
            throw;
        }
    }

    public async Task LoadModelAsync(string modelPath)
    {
        await _loadingSemaphore.WaitAsync();
        
        try
        {
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"Model file not found: {modelPath}");
            }

            _logger.LogInformation("Loading lip-sync model from: {ModelPath}", modelPath);

            // Create session options with optimizations
            var sessionOptions = new SessionOptions
            {
                EnableMemoryPattern = true,
                EnableCpuMemArena = true,
                ExecutionMode = ExecutionMode.ORT_PARALLEL,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };

            // Enable GPU if available
            try
            {
                sessionOptions.AppendExecutionProvider_CUDA(0);
                _logger.LogInformation("CUDA execution provider enabled");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not enable CUDA, falling back to CPU");
            }

            // Dispose existing session
            _session?.Dispose();

            // Load new session
            _session = new InferenceSession(modelPath, sessionOptions);
            
            // Cache the model
            _modelCache.TryAdd(modelPath, _session);

            _logger.LogInformation("Lip-sync model loaded successfully");

            // Validate model inputs/outputs
            await ValidateModelAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load lip-sync model from: {ModelPath}", modelPath);
            throw;
        }
        finally
        {
            _loadingSemaphore.Release();
        }
    }

    public async Task<bool> IsModelLoadedAsync()
    {
        return _session != null;
    }

    public async Task<float[]> GenerateLipMotionAsync(float[] audioFeatures, float[] identityFeatures)
    {
        if (_session == null)
        {
            throw new InvalidOperationException("Model not loaded");
        }

        try
        {
            // Prepare inputs for lip motion generation
            var batchSize = 1;
            var sequenceLength = audioFeatures.Length / 80; // Assuming 80-dim audio features
            var identityDim = identityFeatures.Length;

            // Reshape audio features to sequence
            var audioTensor = new DenseTensor<float>(audioFeatures, new[] { batchSize, sequenceLength, 80 });
            var identityTensor = new DenseTensor<float>(identityFeatures, new[] { batchSize, identityDim });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("audio_sequence", audioTensor),
                NamedOnnxValue.CreateFromTensor("identity_features", identityTensor)
            };

            // Add temporal context if needed
            var contextTensor = CreateTemporalContext(sequenceLength);
            inputs.Add(NamedOnnxValue.CreateFromTensor("temporal_context", contextTensor));

            using var results = _session.Run(inputs);
            var lipMotion = results.FirstOrDefault()?.AsTensor<float>();

            if (lipMotion == null)
            {
                throw new InvalidOperationException("Failed to generate lip motion");
            }

            var output = lipMotion.ToArray();
            
            // Apply post-processing
            return PostProcessLipMotion(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating lip motion");
            throw;
        }
    }

    private async Task ValidateModelAsync()
    {
        if (_session == null) return;

        try
        {
            var inputMetadata = _session.InputMetadata;
            var outputMetadata = _session.OutputMetadata;

            _logger.LogInformation("Model validation:");
            _logger.LogInformation("Inputs: {InputCount}", inputMetadata.Count);
            
            foreach (var input in inputMetadata)
            {
                _logger.LogInformation("  {Name}: {Type} {Shape}", 
                    input.Key, input.Value.ElementType, string.Join("x", input.Value.Dimensions));
            }

            _logger.LogInformation("Outputs: {OutputCount}", outputMetadata.Count);
            
            foreach (var output in outputMetadata)
            {
                _logger.LogInformation("  {Name}: {Type} {Shape}", 
                    output.Key, output.Value.ElementType, string.Join("x", output.Value.Dimensions));
            }

            // Perform a test inference with dummy data
            await PerformTestInferenceAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Model validation failed");
        }
    }

    private async Task PerformTestInferenceAsync()
    {
        try
        {
            // Create dummy inputs based on expected model format
            var dummyAudio = new float[80]; // 80-dim mel features
            var dummyVisual = new float[512]; // 512-dim visual features

            // Fill with small random values
            var random = new Random(42);
            for (int i = 0; i < dummyAudio.Length; i++)
                dummyAudio[i] = (float)(random.NextDouble() * 0.1);
            
            for (int i = 0; i < dummyVisual.Length; i++)
                dummyVisual[i] = (float)(random.NextDouble() * 0.1);

            var result = await PredictAsync(dummyAudio, dummyVisual);
            
            _logger.LogInformation("Test inference successful, output shape: {Length}", result.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Test inference failed");
        }
    }

    private DenseTensor<float> CreateTemporalContext(int sequenceLength)
    {
        // Create temporal context features (position encoding, etc.)
        var contextDim = 64;
        var context = new float[1, sequenceLength, contextDim];
        
        for (int t = 0; t < sequenceLength; t++)
        {
            for (int d = 0; d < contextDim; d++)
            {
                // Simple positional encoding
                if (d % 2 == 0)
                {
                    context[0, t, d] = (float)Math.Sin(t / Math.Pow(10000, 2.0 * d / contextDim));
                }
                else
                {
                    context[0, t, d] = (float)Math.Cos(t / Math.Pow(10000, 2.0 * (d - 1) / contextDim));
                }
            }
        }

        return new DenseTensor<float>(context, new[] { 1, sequenceLength, contextDim });
    }

    private float[] PostProcessLipMotion(float[] rawOutput)
    {
        // Apply smoothing and normalization
        var smoothed = ApplyTemporalSmoothing(rawOutput);
        var normalized = NormalizeLipMotion(smoothed);
        return normalized;
    }

    private float[] ApplyTemporalSmoothing(float[] motion)
    {
        if (motion.Length < 3) return motion;

        var smoothed = new float[motion.Length];
        
        // Apply simple moving average filter
        smoothed[0] = motion[0];
        
        for (int i = 1; i < motion.Length - 1; i++)
        {
            smoothed[i] = (motion[i - 1] + 2 * motion[i] + motion[i + 1]) / 4.0f;
        }
        
        smoothed[motion.Length - 1] = motion[motion.Length - 1];
        
        return smoothed;
    }

    private float[] NormalizeLipMotion(float[] motion)
    {
        // Ensure lip motion parameters are within valid range
        var normalized = new float[motion.Length];
        
        for (int i = 0; i < motion.Length; i++)
        {
            // Clamp to reasonable range and apply sigmoid for smooth transitions
            var clamped = Math.Max(-3.0f, Math.Min(3.0f, motion[i]));
            normalized[i] = (float)(2.0 / (1.0 + Math.Exp(-clamped)) - 1.0);
        }
        
        return normalized;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _session?.Dispose();
            
            foreach (var session in _modelCache.Values)
            {
                session?.Dispose();
            }
            
            _modelCache.Clear();
            _loadingSemaphore?.Dispose();
            _disposed = true;
        }
    }
}