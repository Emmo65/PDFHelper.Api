using Microsoft.AspNetCore.Http;

namespace PdfHelper.Api.Services;

public interface IPdfService
{
    Task<string> MergeAsync(IFormFileCollection files);
    Task<string> SplitAsync(IFormFile file, string ranges);
    Task<string> RotateAsync(IFormFile file, string pages, int angle);
    Task<CompressResult> CompressAsync(IFormFile file, string level);
}

public record CompressResult(bool Success, string? OutputPath, string? ErrorCode, string? Message);
