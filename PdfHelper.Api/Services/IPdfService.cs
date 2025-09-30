using Microsoft.AspNetCore.Http;

namespace PdfHelper.Api.Services
{
    public interface IPdfService
    {
        // --- bestehende Methoden ---
        Task<string> MergeAsync(IFormFileCollection files);
        Task<string> SplitAsync(IFormFile file, string ranges);
        Task<string> RotateAsync(IFormFile file, string pages, int angle);
        Task<CompressResult> CompressAsync(IFormFile file, string level);

        // --- NEU: Textbearbeitung ---
        Task<string> ReplaceTextAsync(IFormFile file, string find, string replace, TextReplaceOptions? options);
        Task<string> RedactTextAsync(IFormFile file, string find, TextReplaceOptions? options);
    }

    // Bestehend aus deiner Datei:
    public record CompressResult(bool Success, string? OutputPath, string? ErrorCode, string? Message);

    // NEU: Optionen für Textsuche/-ersatz
    public record TextReplaceOptions(
        bool MatchCase = false,
        bool WholeWord = true,
        double Padding = 1.5,
        string? FontFamily = null,
        double FontSize = 11.0
    );
}
