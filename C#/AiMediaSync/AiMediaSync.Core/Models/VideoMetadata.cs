namespace AiMediaSync.Core.Models;

/// <summary>
/// Video file metadata information
/// </summary>
public class VideoMetadata
{
    /// <summary>
    /// Video width in pixels
    /// </summary>
    public int Width { get; set; }
    
    /// <summary>
    /// Video height in pixels
    /// </summary>
    public int Height { get; set; }
    
    /// <summary>
    /// Frame rate (frames per second)
    /// </summary>
    public double FrameRate { get; set; }
    
    /// <summary>
    /// Total number of frames
    /// </summary>
    public int TotalFrames { get; set; }
    
    /// <summary>
    /// Video duration
    /// </summary>
    public TimeSpan Duration { get; set; }
    
    /// <summary>
    /// Video codec information
    /// </summary>
    public string Codec { get; set; } = string.Empty;
    
    /// <summary>
    /// Video bit rate
    /// </summary>
    public int BitRate { get; set; }
}