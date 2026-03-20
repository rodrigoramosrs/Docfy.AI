using Docfy.Models;

namespace Docfy.Services;

public interface ILlmVisionService
{
    Task<ImageAnalysisResult> AnalyzeImageAsync(ExtractedImage image);
}
