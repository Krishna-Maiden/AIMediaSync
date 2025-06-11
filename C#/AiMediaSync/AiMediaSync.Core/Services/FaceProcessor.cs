using AiMediaSync.Core.Interfaces;
using AiMediaSync.Core.Models;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace AiMediaSync.Core.Services;

/// <summary>
/// Face processing service implementation
/// </summary>
public class FaceProcessor : IFaceProcessor, IDisposable
{
    private readonly ILogger<FaceProcessor> _logger;
    private readonly Net? _faceDetectionNet;
    private readonly InferenceSession? _landmarkSession;
    private readonly CascadeClassifier? _fallbackDetector;
    private bool _disposed = false;

    public FaceProcessor(ILogger<FaceProcessor> logger, string modelsPath = "Models")
    {
        _logger = logger;
        
        try
        {
            // Initialize face detection DNN
            var prototxt = Path.Combine(modelsPath, "opencv_face_detector.pbtxt");
            var caffeModel = Path.Combine(modelsPath, "opencv_face_detector_uint8.pb");
            
            if (File.Exists(prototxt) && File.Exists(caffeModel))
            {
                _faceDetectionNet = CvDnn.ReadNetFromTensorflow(caffeModel, prototxt);
                _logger.LogInformation("Face detection DNN model loaded successfully");
            }
            else
            {
                _logger.LogWarning("DNN face detection models not found, will use Haar cascade fallback");
            }

            // Initialize facial landmark detection
            var landmarkModel = Path.Combine(modelsPath, "facial_landmarks.onnx");
            if (File.Exists(landmarkModel))
            {
                _landmarkSession = new InferenceSession(landmarkModel);
                _logger.LogInformation("Facial landmark model loaded successfully");
            }

            // Fallback detector
            _fallbackDetector = new CascadeClassifier();
            if (!_fallbackDetector.Load(Cv2.GetDataPath() + "haarcascade_frontalface_default.xml"))
            {
                _logger.LogWarning("Could not load Haar cascade face detector");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing face processor");
            throw;
        }
    }

    public async Task<VisualFeatures> ProcessFrameAsync(byte[] frameData, int width, int height)
    {
        try
        {
            using var mat = Mat.FromImageData(frameData);
            
            var faceBox = await DetectFaceAsync(frameData, width, height);
            if (faceBox == null)
            {
                return new VisualFeatures
                {
                    ConfidenceScore = 0,
                    FaceBoundingBox = null
                };
            }

            var landmarks = await ExtractLandmarksAsync(frameData, faceBox);
            var lipRegion = await ExtractLipRegionAsync(frameData, GetLipBoundingBox(landmarks));

            return new VisualFeatures
            {
                FaceBoundingBox = faceBox,
                LipBoundingBox = GetLipBoundingBox(landmarks),
                FacialLandmarks = landmarks,
                ConfidenceScore = faceBox.Confidence,
                FaceEmbedding = await ExtractFaceEmbeddingAsync(frameData, faceBox),
                LipEmbedding = await ExtractLipEmbeddingAsync(lipRegion)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing frame");
            throw;
        }
    }

    public async Task<BoundingBox?> DetectFaceAsync(byte[] frameData, int width, int height)
    {
        try
        {
            using var mat = Mat.FromImageData(frameData);
            
            if (_faceDetectionNet != null)
            {
                return await DetectFaceWithDNNAsync(mat);
            }
            else
            {
                return await DetectFaceWithHaarAsync(mat);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting face");
            return null;
        }
    }

    public async Task<LandmarkPoints> ExtractLandmarksAsync(byte[] frameData, BoundingBox faceBounds)
    {
        try
        {
            using var mat = Mat.FromImageData(frameData);
            
            if (_landmarkSession == null)
            {
                // Fallback to basic lip region estimation
                return EstimateLipRegion(faceBounds);
            }

            // Prepare face region for landmark detection
            var faceRegion = ExtractFaceRegion(mat, faceBounds);
            using var resizedFace = new Mat();
            Cv2.Resize(faceRegion, resizedFace, new Size(224, 224));

            // Convert to tensor for ONNX model
            var inputTensor = MatToTensor(resizedFace);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", inputTensor)
            };

            using var results = _landmarkSession.Run(inputs);
            var landmarks = results.FirstOrDefault()?.AsTensor<float>();

            if (landmarks != null)
            {
                return ParseLandmarks(landmarks.ToArray(), faceBounds);
            }

            return EstimateLipRegion(faceBounds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting landmarks");
            return EstimateLipRegion(faceBounds);
        }
    }

    public async Task<byte[]> ExtractLipRegionAsync(byte[] frameData, BoundingBox lipBounds, int targetWidth = 96, int targetHeight = 96)
    {
        try
        {
            using var mat = Mat.FromImageData(frameData);
            
            var lipRect = new Rect(lipBounds.X, lipBounds.Y, lipBounds.Width, lipBounds.Height);
            using var lipRegion = new Mat(mat, lipRect);
            
            using var resized = new Mat();
            Cv2.Resize(lipRegion, resized, new Size(targetWidth, targetHeight));
            
            return resized.ToBytes();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting lip region");
            throw;
        }
    }

    private async Task<BoundingBox?> DetectFaceWithDNNAsync(Mat frame)
    {
        const float confidenceThreshold = 0.5f;
        
        using var blob = CvDnn.BlobFromImage(frame, 1.0, new Size(300, 300), new Scalar(104, 117, 123));
        _faceDetectionNet!.SetInput(blob);
        
        using var detections = _faceDetectionNet.Forward();
        
        float maxConfidence = 0;
        BoundingBox? bestBox = null;
        
        for (int i = 0; i < detections.Size(2); i++)
        {
            float confidence = detections.At<float>(0, 0, i, 2);
            
            if (confidence > confidenceThreshold && confidence > maxConfidence)
            {
                maxConfidence = confidence;
                
                int x1 = (int)(detections.At<float>(0, 0, i, 3) * frame.Width);
                int y1 = (int)(detections.At<float>(0, 0, i, 4) * frame.Height);
                int x2 = (int)(detections.At<float>(0, 0, i, 5) * frame.Width);
                int y2 = (int)(detections.At<float>(0, 0, i, 6) * frame.Height);
                
                bestBox = new BoundingBox
                {
                    X = x1,
                    Y = y1,
                    Width = x2 - x1,
                    Height = y2 - y1,
                    Confidence = confidence
                };
            }
        }
        
        return bestBox;
    }

    private async Task<BoundingBox?> DetectFaceWithHaarAsync(Mat frame)
    {
        if (_fallbackDetector == null)
            return null;
            
        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        
        var faces = _fallbackDetector.DetectMultiScale(gray, 1.1, 4);
        
        if (faces.Length > 0)
        {
            var face = faces.OrderByDescending(f => f.Width * f.Height).First();
            return new BoundingBox
            {
                X = face.X,
                Y = face.Y,
                Width = face.Width,
                Height = face.Height,
                Confidence = 0.8f // Haar cascade doesn't provide confidence
            };
        }
        
        return null;
    }

    private async Task<float[]> ExtractFaceEmbeddingAsync(byte[] frameData, BoundingBox faceBounds)
    {
        // Placeholder for face embedding extraction
        // In production, this would use a face recognition model
        var embedding = new float[512];
        var random = new Random();
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1); // Random values between -1 and 1
        }
        return embedding;
    }

    private async Task<float[]> ExtractLipEmbeddingAsync(byte[] lipRegionData)
    {
        // Placeholder for lip embedding extraction
        // In production, this would use a specialized lip feature extraction model
        var embedding = new float[256];
        var random = new Random();
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1); // Random values between -1 and 1
        }
        return embedding;
    }

    private BoundingBox GetLipBoundingBox(LandmarkPoints landmarks)
    {
        if (landmarks?.LipPoints == null || landmarks.LipPoints.Length == 0)
        {
            return new BoundingBox { X = 0, Y = 0, Width = 96, Height = 96, Confidence = 0.5f };
        }

        var minX = landmarks.LipPoints.Min(p => p.X);
        var maxX = landmarks.LipPoints.Max(p => p.X);
        var minY = landmarks.LipPoints.Min(p => p.Y);
        var maxY = landmarks.LipPoints.Max(p => p.Y);

        return new BoundingBox
        {
            X = (int)minX,
            Y = (int)minY,
            Width = (int)(maxX - minX),
            Height = (int)(maxY - minY),
            Confidence = 0.9f
        };
    }

    private LandmarkPoints EstimateLipRegion(BoundingBox faceBounds)
    {
        // Fallback estimation when landmark detection is not available
        var lipPoints = new Point2D[20];
        
        // Estimate lip region as bottom third of face
        var lipCenterX = faceBounds.X + faceBounds.Width / 2f;
        var lipCenterY = faceBounds.Y + faceBounds.Height * 0.75f;
        var lipWidth = faceBounds.Width * 0.4f;
        var lipHeight = faceBounds.Height * 0.15f;

        // Create simple lip contour points
        for (int i = 0; i < lipPoints.Length; i++)
        {
            var angle = 2 * Math.PI * i / lipPoints.Length;
            lipPoints[i] = new Point2D(
                lipCenterX + (float)(Math.Cos(angle) * lipWidth / 2),
                lipCenterY + (float)(Math.Sin(angle) * lipHeight / 2)
            );
        }

        return new LandmarkPoints
        {
            LipPoints = lipPoints,
            FaceContour = Array.Empty<Point2D>(),
            EyePoints = Array.Empty<Point2D>(),
            NosePoints = Array.Empty<Point2D>()
        };
    }

    private Mat ExtractFaceRegion(Mat frame, BoundingBox faceBounds)
    {
        var faceRect = new Rect(faceBounds.X, faceBounds.Y, faceBounds.Width, faceBounds.Height);
        return new Mat(frame, faceRect);
    }

    private Tensor<float> MatToTensor(Mat mat)
    {
        var data = new float[1 * 3 * mat.Height * mat.Width];
        var bytes = new byte[mat.Total() * mat.ElemSize()];
        mat.GetArray(out bytes);

        // Convert BGR to RGB and normalize
        for (int i = 0; i < bytes.Length; i += 3)
        {
            var idx = i / 3;
            data[idx] = bytes[i + 2] / 255f; // R
            data[idx + mat.Height * mat.Width] = bytes[i + 1] / 255f; // G
            data[idx + 2 * mat.Height * mat.Width] = bytes[i] / 255f; // B
        }

        return new DenseTensor<float>(data, new[] { 1, 3, mat.Height, mat.Width });
    }

    private LandmarkPoints ParseLandmarks(float[] landmarks, BoundingBox faceBounds)
    {
        var lipPoints = new Point2D[20];
        var faceContour = new Point2D[17];
        
        // Parse landmark points based on model output format
        // This is a simplified version - actual implementation depends on the model
        for (int i = 0; i < lipPoints.Length && i * 2 + 1 < landmarks.Length; i++)
        {
            lipPoints[i] = new Point2D(
                faceBounds.X + landmarks[i * 2] * faceBounds.Width,
                faceBounds.Y + landmarks[i * 2 + 1] * faceBounds.Height
            );
        }

        return new LandmarkPoints
        {
            LipPoints = lipPoints,
            FaceContour = faceContour,
            EyePoints = new Point2D[12],
            NosePoints = new Point2D[9]
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _faceDetectionNet?.Dispose();
            _landmarkSession?.Dispose();
            _fallbackDetector?.Dispose();
            _disposed = true;
        }
    }
}