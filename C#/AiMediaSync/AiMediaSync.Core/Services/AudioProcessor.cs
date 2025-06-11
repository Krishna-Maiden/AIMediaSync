using AiMediaSync.Core.Interfaces;
using AiMediaSync.Core.Models;
using NAudio.Wave;
using Microsoft.Extensions.Logging;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;

namespace AiMediaSync.Core.Services;

/// <summary>
/// Audio processing service implementation
/// </summary>
public class AudioProcessor : IAudioProcessor
{
    private readonly ILogger<AudioProcessor> _logger;
    
    public AudioProcessor(ILogger<AudioProcessor> logger)
    {
        _logger = logger;
    }

    public async Task<float[]> LoadAudioAsync(string audioPath)
    {
        try
        {
            _logger.LogInformation($"Loading audio from: {audioPath}");
            
            using var audioFile = new AudioFileReader(audioPath);
            var samples = new List<float>();
            var buffer = new float[4096];
            int samplesRead;
            
            while ((samplesRead = audioFile.Read(buffer, 0, buffer.Length)) > 0)
            {
                samples.AddRange(buffer.Take(samplesRead));
            }
            
            _logger.LogInformation($"Loaded {samples.Count} audio samples");
            return samples.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error loading audio from {audioPath}");
            throw;
        }
    }

    public async Task<AudioFeatures> ExtractFeaturesAsync(float[] audioData, int sampleRate = 16000)
    {
        try
        {
            _logger.LogInformation("Extracting audio features");
            
            var features = new AudioFeatures
            {
                SampleRate = sampleRate,
                AudioLength = audioData.Length / (float)sampleRate
            };

            // Extract MFCC features
            features.MFCC = await ExtractMFCCAsync(audioData, sampleRate);
            
            // Extract Mel Spectrogram
            features.MelSpectrogram = await ExtractMelSpectrogramAsync(audioData, sampleRate);
            
            // Extract Spectral Centroid
            features.SpectralCentroid = await ExtractSpectralCentroidAsync(audioData, sampleRate);
            
            // Extract Chroma Features
            features.ChromaFeatures = await ExtractChromaFeaturesAsync(audioData, sampleRate);
            
            // Extract Spectral Rolloff
            features.SpectralRolloff = await ExtractSpectralRolloffAsync(audioData, sampleRate);
            
            // Extract Zero Crossing Rate
            features.ZeroCrossingRate = await ExtractZeroCrossingRateAsync(audioData, sampleRate);

            _logger.LogInformation("Audio feature extraction completed");
            return features;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting audio features");
            throw;
        }
    }

    public async Task<float[]> ResampleAudioAsync(float[] audio, int originalSampleRate, int targetSampleRate)
    {
        if (originalSampleRate == targetSampleRate)
            return audio;

        try
        {
            _logger.LogInformation($"Resampling audio from {originalSampleRate}Hz to {targetSampleRate}Hz");
            
            double ratio = (double)targetSampleRate / originalSampleRate;
            int newLength = (int)(audio.Length * ratio);
            var resampled = new float[newLength];

            // Linear interpolation resampling
            for (int i = 0; i < newLength; i++)
            {
                double sourceIndex = i / ratio;
                int index = (int)sourceIndex;
                double fraction = sourceIndex - index;
                
                if (index < audio.Length - 1)
                {
                    resampled[i] = (float)(audio[index] * (1 - fraction) + audio[index + 1] * fraction);
                }
                else if (index < audio.Length)
                {
                    resampled[i] = audio[index];
                }
            }

            return resampled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resampling audio");
            throw;
        }
    }

    public async Task<float[,]> AlignAudioWithVideoAsync(AudioFeatures audioFeatures, int videoFrameCount, double videoFps)
    {
        try
        {
            _logger.LogInformation($"Aligning audio features with {videoFrameCount} video frames at {videoFps} FPS");
            
            var melSpec = audioFeatures.MelSpectrogram;
            int melFrames = melSpec.GetLength(1);
            int melBins = melSpec.GetLength(0);
            
            var alignedFeatures = new float[videoFrameCount, melBins];

            for (int videoFrame = 0; videoFrame < videoFrameCount; videoFrame++)
            {
                // Calculate corresponding audio frame
                double timeInSeconds = videoFrame / videoFps;
                int audioFrame = (int)(timeInSeconds * melFrames / audioFeatures.AudioLength);
                audioFrame = Math.Min(audioFrame, melFrames - 1);

                for (int bin = 0; bin < melBins; bin++)
                {
                    alignedFeatures[videoFrame, bin] = melSpec[bin, audioFrame];
                }
            }

            _logger.LogInformation("Audio-video alignment completed");
            return alignedFeatures;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aligning audio with video");
            throw;
        }
    }

    private async Task<float[,]> ExtractMFCCAsync(float[] audio, int sampleRate)
    {
        // MFCC implementation using proper signal processing
        int frameSize = 2048;
        int hopSize = 512;
        int numMfcc = 13;
        int numMelFilters = 26;
        
        var frames = CreateFrames(audio, frameSize, hopSize);
        var mfcc = new float[numMfcc, frames.Count];
        
        for (int i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            
            // Apply window function
            ApplyHammingWindow(frame);
            
            // FFT
            var spectrum = ComputeFFT(frame);
            
            // Mel filter bank
            var melSpectrum = ApplyMelFilterBank(spectrum, sampleRate, numMelFilters);
            
            // Log and DCT
            var logMel = melSpectrum.Select(x => (float)Math.Log(Math.Max(x, 1e-10))).ToArray();
            var mfccFrame = ComputeDCT(logMel, numMfcc);
            
            for (int j = 0; j < numMfcc; j++)
            {
                mfcc[j, i] = mfccFrame[j];
            }
        }
        
        return mfcc;
    }

    private async Task<float[,]> ExtractMelSpectrogramAsync(float[] audio, int sampleRate)
    {
        int frameSize = 2048;
        int hopSize = 512;
        int numMelFilters = 80;
        
        var frames = CreateFrames(audio, frameSize, hopSize);
        var melSpectrogram = new float[numMelFilters, frames.Count];
        
        for (int i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            ApplyHammingWindow(frame);
            var spectrum = ComputeFFT(frame);
            var melSpectrum = ApplyMelFilterBank(spectrum, sampleRate, numMelFilters);
            
            for (int j = 0; j < numMelFilters; j++)
            {
                melSpectrogram[j, i] = (float)Math.Log(Math.Max(melSpectrum[j], 1e-10));
            }
        }
        
        return melSpectrogram;
    }

    private async Task<float[]> ExtractSpectralCentroidAsync(float[] audio, int sampleRate)
    {
        int frameSize = 2048;
        int hopSize = 512;
        
        var frames = CreateFrames(audio, frameSize, hopSize);
        var centroids = new float[frames.Count];
        
        for (int i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            ApplyHammingWindow(frame);
            var spectrum = ComputeFFT(frame);
            
            float numerator = 0;
            float denominator = 0;
            
            for (int k = 0; k < spectrum.Length; k++)
            {
                float freq = k * sampleRate / (2.0f * spectrum.Length);
                float magnitude = spectrum[k];
                numerator += freq * magnitude;
                denominator += magnitude;
            }
            
            centroids[i] = denominator > 0 ? numerator / denominator : 0;
        }
        
        return centroids;
    }

    private async Task<float[]> ExtractChromaFeaturesAsync(float[] audio, int sampleRate)
    {
        // Simplified chroma feature extraction
        int frameSize = 2048;
        int hopSize = 512;
        int numChroma = 12;
        
        var frames = CreateFrames(audio, frameSize, hopSize);
        var chromaFeatures = new float[numChroma * frames.Count];
        
        for (int i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            ApplyHammingWindow(frame);
            var spectrum = ComputeFFT(frame);
            var chroma = ComputeChroma(spectrum, sampleRate);
            
            for (int j = 0; j < numChroma; j++)
            {
                chromaFeatures[i * numChroma + j] = chroma[j];
            }
        }
        
        return chromaFeatures;
    }

    private async Task<float[]> ExtractSpectralRolloffAsync(float[] audio, int sampleRate)
    {
        int frameSize = 2048;
        int hopSize = 512;
        float rolloffThreshold = 0.85f;
        
        var frames = CreateFrames(audio, frameSize, hopSize);
        var rolloffs = new float[frames.Count];
        
        for (int i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            ApplyHammingWindow(frame);
            var spectrum = ComputeFFT(frame);
            
            float totalEnergy = spectrum.Sum();
            float cumulativeEnergy = 0;
            
            for (int k = 0; k < spectrum.Length; k++)
            {
                cumulativeEnergy += spectrum[k];
                if (cumulativeEnergy >= rolloffThreshold * totalEnergy)
                {
                    rolloffs[i] = k * sampleRate / (2.0f * spectrum.Length);
                    break;
                }
            }
        }
        
        return rolloffs;
    }

    private async Task<float[]> ExtractZeroCrossingRateAsync(float[] audio, int sampleRate)
    {
        int frameSize = 2048;
        int hopSize = 512;
        
        var frames = CreateFrames(audio, frameSize, hopSize);
        var zcr = new float[frames.Count];
        
        for (int i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            int crossings = 0;
            
            for (int j = 1; j < frame.Length; j++)
            {
                if ((frame[j] >= 0) != (frame[j - 1] >= 0))
                    crossings++;
            }
            
            zcr[i] = crossings / (float)(frame.Length - 1);
        }
        
        return zcr;
    }

    private List<float[]> CreateFrames(float[] audio, int frameSize, int hopSize)
    {
        var frames = new List<float[]>();
        
        for (int i = 0; i <= audio.Length - frameSize; i += hopSize)
        {
            var frame = new float[frameSize];
            Array.Copy(audio, i, frame, 0, frameSize);
            frames.Add(frame);
        }
        
        return frames;
    }

    private void ApplyHammingWindow(float[] frame)
    {
        for (int i = 0; i < frame.Length; i++)
        {
            frame[i] *= (float)(0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (frame.Length - 1)));
        }
    }

    private float[] ComputeFFT(float[] frame)
    {
        var complex = frame.Select(x => new System.Numerics.Complex(x, 0)).ToArray();
        Fourier.Forward(complex, FourierOptions.NoScaling);
        return complex.Select(x => (float)x.Magnitude).ToArray();
    }

    private float[] ApplyMelFilterBank(float[] spectrum, int sampleRate, int numFilters)
    {
        // Mel filter bank implementation
        float melMin = 0;
        float melMax = 2595 * (float)Math.Log10(1 + sampleRate / 2.0 / 700);
        
        var melPoints = new float[numFilters + 2];
        for (int i = 0; i < melPoints.Length; i++)
        {
            melPoints[i] = melMin + i * (melMax - melMin) / (melPoints.Length - 1);
        }
        
        var freqPoints = melPoints.Select(mel => 700 * ((float)Math.Pow(10, mel / 2595) - 1)).ToArray();
        var binPoints = freqPoints.Select(freq => (int)Math.Floor(freq * spectrum.Length * 2 / sampleRate)).ToArray();
        
        var melSpectrum = new float[numFilters];
        
        for (int i = 1; i <= numFilters; i++)
        {
            int left = binPoints[i - 1];
            int center = binPoints[i];
            int right = binPoints[i + 1];
            
            for (int k = left; k < center; k++)
            {
                if (k < spectrum.Length)
                    melSpectrum[i - 1] += spectrum[k] * (k - left) / (float)(center - left);
            }
            
            for (int k = center; k < right; k++)
            {
                if (k < spectrum.Length)
                    melSpectrum[i - 1] += spectrum[k] * (right - k) / (float)(right - center);
            }
        }
        
        return melSpectrum;
    }

    private float[] ComputeDCT(float[] input, int numCoeffs)
    {
        var output = new float[numCoeffs];
        
        for (int k = 0; k < numCoeffs; k++)
        {
            float sum = 0;
            for (int n = 0; n < input.Length; n++)
            {
                sum += input[n] * (float)Math.Cos(Math.PI * k * (n + 0.5) / input.Length);
            }
            output[k] = sum;
        }
        
        return output;
    }

    private float[] ComputeChroma(float[] spectrum, int sampleRate)
    {
        var chroma = new float[12];
        
        for (int k = 1; k < spectrum.Length; k++)
        {
            float freq = k * sampleRate / (2.0f * spectrum.Length);
            if (freq > 0)
            {
                int chromaBin = (int)(12 * Math.Log(freq / 440.0) / Math.Log(2)) % 12;
                if (chromaBin < 0) chromaBin += 12;
                chroma[chromaBin] += spectrum[k];
            }
        }
        
        // Normalize
        float sum = chroma.Sum();
        if (sum > 0)
        {
            for (int i = 0; i < 12; i++)
                chroma[i] /= sum;
        }
        
        return chroma;
    }
}