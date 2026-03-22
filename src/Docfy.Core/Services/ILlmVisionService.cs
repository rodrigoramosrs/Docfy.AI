using Docfy.Core.Models;

namespace Docfy.Core.Services;

public interface ILlmVisionService
{
    Task<ImageAnalysisResult> AnalyzeImageAsync(ExtractedImage image);
}
