namespace CmsBillboard.DynamicContent.Services;

public class DetectedFaceDto
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double CenterX { get; set; }
    public double CenterY { get; set; }
}

public class FaceDetectionResultDto
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public int FaceCount { get; set; }
    public List<DetectedFaceDto> Faces { get; set; } = new();
}

public interface IFaceDetectionService
{
    Task<FaceDetectionResultDto> DetectFacesAsync(string imagePhysicalPath);
    Task<string?> ProcessUserPortraitAsync(string inputPhysicalPath, string outputPhysicalPath);
}
