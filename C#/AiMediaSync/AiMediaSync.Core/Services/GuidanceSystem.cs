using AiMediaSync.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AiMediaSync.Core.Services;

/// <summary>
/// Dynamic guidance system implementation
/// </summary>
public class GuidanceSystem : IGuidanceSystem
{
    private readonly ILogger<GuidanceSystem> _logger;

    public GuidanceSystem(ILogger<GuidanceSystem> logger)
    {
        _logger = logger;
    }

    public float ComputeGuidanceStrength(float audioPower, int frameIndex, int totalFrames, float baseStrength = 0.7f)
    {
        try
        {
            // Temporal weighting - stronger guidance in the middle of the sequence
            var normalizedTime = (float)frameIndex / totalFrames;
            var temporalWeight = 1.0f - Math.Abs(normalizedTime - 0.5f) * 0.4f;

            // Audio power weighting - stronger guidance for louder audio
            var audioPowerNormalized = Math.Min(audioPower * 3.0f, 1.0f);
            var audioWeight = 0.5f + audioPowerNormalized * 0.5f;

            // Speech activity detection - boost guidance during active speech
            var speechActivity = DetectSpeechActivity(audioPower);
            var speechWeight = speechActivity ? 1.2f : 0.8f;

            // Combine all weights
            var finalStrength = baseStrength * temporalWeight * audioWeight * speechWeight;
            
            // Clamp to valid range
            return Math.Max(0.1f, Math.Min(1.0f, finalStrength));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error computing guidance strength, using base strength");
            return baseStrength;
        }
    }

    public float ComputeTemporalConsistency(float[] currentFeatures, float[] previousFeatures)
    {
        try
        {
            if (currentFeatures == null || previousFeatures == null)
                return 0.0f;

            if (currentFeatures.Length != previousFeatures.Length)
                return 0.0f;

            // Compute cosine similarity
            var dotProduct = 0.0f;
            var currentMagnitude = 0.0f;
            var previousMagnitude = 0.0f;

            for (int i = 0; i < currentFeatures.Length; i++)
            {
                dotProduct += currentFeatures[i] * previousFeatures[i];
                currentMagnitude += currentFeatures[i] * currentFeatures[i];
                previousMagnitude += previousFeatures[i] * previousFeatures[i];
            }

            currentMagnitude = (float)Math.Sqrt(currentMagnitude);
            previousMagnitude = (float)Math.Sqrt(previousMagnitude);

            if (currentMagnitude == 0 || previousMagnitude == 0)
                return 0.0f;

            var similarity = dotProduct / (currentMagnitude * previousMagnitude);
            return Math.Max(0.0f, Math.Min(1.0f, similarity));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error computing temporal consistency");
            return 0.0f;
        }
    }

    public float AdaptiveGuidanceScaling(float audioEnergy, float visualComplexity)
    {
        try
        {
            // Scale guidance based on audio energy and visual complexity
            var energyFactor = Math.Min(audioEnergy * 2.0f, 1.0f);
            var complexityFactor = Math.Min(visualComplexity * 1.5f, 1.0f);

            // Higher energy or complexity requires stronger guidance
            var scalingFactor = 0.5f + (energyFactor + complexityFactor) * 0.25f;

            return Math.Max(0.3f, Math.Min(1.5f, scalingFactor));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error computing adaptive guidance scaling");
            return 1.0f;
        }
    }

    private bool DetectSpeechActivity(float audioPower)
    {
        // Simple voice activity detection based on audio power
        const float speechThreshold = 0.01f;
        return audioPower > speechThreshold;
    }
}