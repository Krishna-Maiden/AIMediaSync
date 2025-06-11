using System;

namespace AiMediaSync.Core.Extensions;

/// <summary>
/// Extension methods for Random class to generate various distributions
/// </summary>
public static class RandomExtensions
{
    /// <summary>
    /// Generate a random number from Gaussian (normal) distribution
    /// </summary>
    /// <param name="random">Random instance</param>
    /// <param name="mean">Mean of the distribution</param>
    /// <param name="stdDev">Standard deviation of the distribution</param>
    /// <returns>Random value from Gaussian distribution</returns>
    public static double NextGaussian(this Random random, double mean = 0, double stdDev = 1)
    {
        // Box-Muller transform for generating Gaussian random numbers
        static double NextGaussianStandard(Random rnd)
        {
            double u1 = 1.0 - rnd.NextDouble(); // uniform(0,1] random doubles
            double u2 = 1.0 - rnd.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }

        return mean + stdDev * NextGaussianStandard(random);
    }

    /// <summary>
    /// Generate a random float from Gaussian distribution
    /// </summary>
    /// <param name="random">Random instance</param>
    /// <param name="mean">Mean of the distribution</param>
    /// <param name="stdDev">Standard deviation of the distribution</param>
    /// <returns>Random float value from Gaussian distribution</returns>
    public static float NextGaussianFloat(this Random random, float mean = 0f, float stdDev = 1f)
    {
        return (float)NextGaussian(random, mean, stdDev);
    }

    /// <summary>
    /// Generate random bytes with Gaussian distribution
    /// </summary>
    /// <param name="random">Random instance</param>
    /// <param name="buffer">Buffer to fill with random bytes</param>
    /// <param name="mean">Mean of the distribution</param>
    /// <param name="stdDev">Standard deviation of the distribution</param>
    public static void NextGaussianBytes(this Random random, byte[] buffer, double mean = 127.5, double stdDev = 50)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            var value = random.NextGaussian(mean, stdDev);
            buffer[i] = (byte)Math.Max(0, Math.Min(255, Math.Round(value)));
        }
    }

    /// <summary>
    /// Generate a random value from uniform distribution within specified range
    /// </summary>
    /// <param name="random">Random instance</param>
    /// <param name="min">Minimum value (inclusive)</param>
    /// <param name="max">Maximum value (exclusive)</param>
    /// <returns>Random float value in range [min, max)</returns>
    public static float NextFloat(this Random random, float min = 0f, float max = 1f)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    /// <summary>
    /// Generate a random boolean with specified probability
    /// </summary>
    /// <param name="random">Random instance</param>
    /// <param name="probability">Probability of returning true (0.0 to 1.0)</param>
    /// <returns>Random boolean value</returns>
    public static bool NextBoolean(this Random random, double probability = 0.5)
    {
        return random.NextDouble() < probability;
    }

    /// <summary>
    /// Select a random element from an array
    /// </summary>
    /// <typeparam name="T">Type of array elements</typeparam>
    /// <param name="random">Random instance</param>
    /// <param name="array">Array to select from</param>
    /// <returns>Random element from the array</returns>
    public static T NextElement<T>(this Random random, T[] array)
    {
        if (array == null || array.Length == 0)
            throw new ArgumentException("Array cannot be null or empty", nameof(array));
        
        return array[random.Next(array.Length)];
    }

    /// <summary>
    /// Generate a random color in RGB format
    /// </summary>
    /// <param name="random">Random instance</param>
    /// <returns>Tuple representing RGB color values (0-255)</returns>
    public static (byte R, byte G, byte B) NextColor(this Random random)
    {
        return ((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
    }
}