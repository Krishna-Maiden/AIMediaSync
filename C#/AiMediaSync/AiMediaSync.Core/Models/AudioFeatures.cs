namespace AiMediaSync.Core.Models;

/// <summary>
/// Represents extracted audio features for lip-sync processing
/// </summary>
public class AudioFeatures
{
    /// <summary>
    /// Mel-Frequency Cepstral Coefficients
    /// </summary>
    public float[,] MFCC { get; set; } = new float[0,0];
    
    /// <summary>
    /// Mel spectrogram features
    /// </summary>
    public float[,] MelSpectrogram { get; set; } = new float[0,0];
    
    /// <summary>
    /// Spectral centroid values
    /// </summary>
    public float[] SpectralCentroid { get; set; } = Array.Empty<float>();
    
    /// <summary>
    /// Chroma features for musical content
    /// </summary>
    public float[] ChromaFeatures { get; set; } = Array.Empty<float>();
    
    /// <summary>
    /// Spectral rolloff values
    /// </summary>
    public float[] SpectralRolloff { get; set; } = Array.Empty<float>();
    
    /// <summary>
    /// Zero crossing rate values
    /// </summary>
    public float[] ZeroCrossingRate { get; set; } = Array.Empty<float>();
    
    /// <summary>
    /// Audio length in seconds
    /// </summary>
    public float AudioLength { get; set; }
    
    /// <summary>
    /// Sample rate of the audio
    /// </summary>
    public int SampleRate { get; set; }
    
    /// <summary>
    /// Timestamp when features were processed
    /// </summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}