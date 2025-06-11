namespace AiMediaSync.Core.Models;

/// <summary>
/// Visual features extracted from video frames
/// </summary>
public class VisualFeatures
{
    /// <summary>
    /// Face embedding vector
    /// </summary>
    public float[] FaceEmbedding { get; set; } = Array.Empty<float>();
    
    /// <summary>
    /// Lip region embedding vector
    /// </summary>
    public float[] LipEmbedding { get; set; } = Array.Empty<float>();
    
    /// <summary>
    /// Face bounding box coordinates
    /// </summary>
    public BoundingBox? FaceBoundingBox { get; set; }
    
    /// <summary>
    /// Lip region bounding box coordinates
    /// </summary>
    public BoundingBox? LipBoundingBox { get; set; }
    
    /// <summary>
    /// Facial landmark points
    /// </summary>
    public LandmarkPoints? FacialLandmarks { get; set; }
    
    /// <summary>
    /// Confidence score of face detection
    /// </summary>
    public float ConfidenceScore { get; set; }
    
    /// <summary>
    /// Frame index in the video sequence
    /// </summary>
    public int FrameIndex { get; set; }
}

/// <summary>
/// Bounding box coordinates
/// </summary>
public class BoundingBox
{
    /// <summary>
    /// X coordinate of top-left corner
    /// </summary>
    public int X { get; set; }
    
    /// <summary>
    /// Y coordinate of top-left corner
    /// </summary>
    public int Y { get; set; }
    
    /// <summary>
    /// Width of the bounding box
    /// </summary>
    public int Width { get; set; }
    
    /// <summary>
    /// Height of the bounding box
    /// </summary>
    public int Height { get; set; }
    
    /// <summary>
    /// Confidence score of the detection
    /// </summary>
    public float Confidence { get; set; }
}

/// <summary>
/// Facial landmark points
/// </summary>
public class LandmarkPoints
{
    /// <summary>
    /// Lip contour points
    /// </summary>
    public Point2D[] LipPoints { get; set; } = Array.Empty<Point2D>();
    
    /// <summary>
    /// Face contour points
    /// </summary>
    public Point2D[] FaceContour { get; set; } = Array.Empty<Point2D>();
    
    /// <summary>
    /// Eye region points
    /// </summary>
    public Point2D[] EyePoints { get; set; } = Array.Empty<Point2D>();
    
    /// <summary>
    /// Nose region points
    /// </summary>
    public Point2D[] NosePoints { get; set; } = Array.Empty<Point2D>();
}

/// <summary>
/// 2D point structure
/// </summary>
public struct Point2D
{
    /// <summary>
    /// X coordinate
    /// </summary>
    public float X { get; set; }
    
    /// <summary>
    /// Y coordinate
    /// </summary>
    public float Y { get; set; }
    
    /// <summary>
    /// Initialize a new Point2D
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    public Point2D(float x, float y)
    {
        X = x;
        Y = y;
    }
}