using System.Diagnostics;
using System.Text.Json;

namespace CmsBillboard.DynamicContent.Services;

public class FaceDetectionService : IFaceDetectionService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<FaceDetectionService> _logger;

    public FaceDetectionService(IWebHostEnvironment env, ILogger<FaceDetectionService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<FaceDetectionResultDto> DetectFacesAsync(string imagePhysicalPath)
    {
        if (!File.Exists(imagePhysicalPath))
        {
            return new FaceDetectionResultDto { Success = false, Error = $"File tidak ditemukan: {imagePhysicalPath}" };
        }

        var scriptPath = Path.Combine(_env.ContentRootPath, "scripts", "detect_face.py");
        if (!File.Exists(scriptPath))
        {
            return new FaceDetectionResultDto { Success = false, Error = $"Script deteksi wajah tidak ditemukan: {scriptPath}" };
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{scriptPath}\" \"{imagePhysicalPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            var completed = await Task.WhenAny(Task.Delay(5000), process.WaitForExitAsync());
            if (completed is not Task<bool> && process.HasExited == false)
            {
                process.Kill(true);
                return new FaceDetectionResultDto { Success = false, Error = "Timeout saat mendeteksi wajah." };
            }

            var output = await outputTask;
            var error = await errorTask;

            if (!string.IsNullOrWhiteSpace(output))
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<FaceDetectionResultDto>(output.Trim(), options);
                if (result != null)
                {
                    return result;
                }
            }

            _logger.LogWarning("Face detection error: {Error}", error);
            return new FaceDetectionResultDto { Success = false, Error = error };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run face detection");
            return new FaceDetectionResultDto { Success = false, Error = ex.Message };
        }
    }
}
