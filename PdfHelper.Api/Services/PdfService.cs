using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using PdfSharpCore.Pdf.IO;
using PdfSharpCore.Drawing;
using UglyToad.PdfPig.Content;

// Alias, damit kein Konflikt mit UglyToad.PdfPig.PdfDocument entsteht:
using SharpPdfDocument = PdfSharpCore.Pdf.PdfDocument;

namespace PdfHelper.Api.Services;

public class PdfService : IPdfService
{
    private readonly string _root;

    public PdfService()
    {
        _root = Path.Combine(Path.GetTempPath(), "pdfhelper");
        Directory.CreateDirectory(_root);
    }

    public async Task<string> MergeAsync(IFormFileCollection files)
    {
        var inputs = await SaveUploads(files);
        var outPath = TempPath("merged", ".pdf");

        using var output = new SharpPdfDocument();
        foreach (var inPath in inputs)
        {
            using var input = PdfReader.Open(inPath, PdfDocumentOpenMode.Import);
            for (int i = 0; i < input.PageCount; i++)
                output.AddPage(input.Pages[i]);
        }
        output.Save(outPath);
        return outPath;
    }

    public async Task<string> SplitAsync(IFormFile file, string ranges)
    {
        var inPath = await SaveUpload(file);
        using var doc = PdfReader.Open(inPath, PdfDocumentOpenMode.Import);
        var partsDir = Path.Combine(_root, $"parts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(partsDir);

        var parsed = ParseRanges(ranges, doc.PageCount); // (start,end), 1-basiert
        int idx = 1;
        foreach (var (start, end) in parsed)
        {
            using var part = new SharpPdfDocument();
            for (int p = start; p <= end; p++)
                part.AddPage(doc.Pages[p - 1]);
            var partPath = Path.Combine(partsDir, $"part_{idx++}_{start}-{end}.pdf");
            part.Save(partPath);
        }

        var zipPath = TempPath("parts", ".zip");
        ZipFile.CreateFromDirectory(partsDir, zipPath);
        return zipPath;
    }

    public async Task<string> RotateAsync(IFormFile file, string pages, int angle)
    {
        var inPath = await SaveUpload(file);
        var outPath = TempPath("rotated", ".pdf");

        using var input = PdfReader.Open(inPath, PdfDocumentOpenMode.Import);
        using var output = new SharpPdfDocument();

        var targetPages = ParsePageList(pages, input.PageCount); // 1-basiert

        for (int i = 0; i < input.PageCount; i++)
        {
            var src = input.Pages[i];
            var dst = output.AddPage(src);
            int pageNo = i + 1;
            if (targetPages.Contains(pageNo))
            {
                dst.Rotate = (dst.Rotate + angle) % 360;
                if (dst.Rotate < 0) dst.Rotate += 360;
            }
        }

        output.Save(outPath);
        return outPath;
    }

    public async Task<CompressResult> CompressAsync(IFormFile file, string level)
    {
        var inPath = await SaveUpload(file);
        var outPath = TempPath("compressed", ".pdf");

        var gs = FindGhostscript();
        if (gs is null)
            return new CompressResult(false, null, "NO_COMPRESSOR",
                "Ghostscript nicht gefunden. Installiere es (gswin64c.exe), oder nutze die anderen Funktionen.");

        var preset = level.ToLower() switch
        {
            "low" or "screen" => "/screen",
            "high" or "prepress" => "/prepress",
            _ => "/ebook" // standard
        };

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = gs,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-sDEVICE=pdfwrite");
        psi.ArgumentList.Add("-dCompatibilityLevel=1.5");
        psi.ArgumentList.Add("-dNOPAUSE"); psi.ArgumentList.Add("-dQUIET"); psi.ArgumentList.Add("-dBATCH");
        psi.ArgumentList.Add($"-dPDFSETTINGS={preset}");
        psi.ArgumentList.Add($"-sOutputFile={outPath}");
        psi.ArgumentList.Add(inPath);

        try
        {
            using var p = System.Diagnostics.Process.Start(psi)!;
            await p.WaitForExitAsync();
            if (p.ExitCode != 0 || !File.Exists(outPath))
            {
                var err = await p.StandardError.ReadToEndAsync();
                return new CompressResult(false, null, "COMPRESS_FAILED", $"Ghostscript-Fehler: {err}");
            }
            return new CompressResult(true, outPath, null, null);
        }
        catch (Exception ex)
        {
            return new CompressResult(false, null, "COMPRESS_EXCEPTION", ex.Message);
        }
    }

    // ========== NEU: Textbearbeitung ==========

    public async Task<string> ReplaceTextAsync(IFormFile file, string find, string replace, TextReplaceOptions? options)
    {
        if (string.IsNullOrWhiteSpace(find)) throw new ArgumentException("Suchtext darf nicht leer sein.");
        options ??= new TextReplaceOptions();

        var inPath = await SaveUpload(file);
        var outPath = TempPath("replace", ".pdf");

        var matches = FindTextOccurrences(inPath, find, options);

        using var input = PdfReader.Open(inPath, PdfDocumentOpenMode.Import);
        using var output = new SharpPdfDocument();

        for (int i = 0; i < input.PageCount; i++)
        {
            var src = input.Pages[i];
            var dst = output.AddPage(src);
            using var gfx = XGraphics.FromPdfPage(dst);

            var font = new XFont(options.FontFamily ?? "Arial", options.FontSize, XFontStyle.Regular);

            if (matches.TryGetValue(i + 1, out var rects))
            {
                foreach (var r in rects)
                {
                    var pad = options.Padding;
                    var rect = new XRect(r.X - pad, dst.Height - (r.Y + r.Height) - pad, r.Width + 2 * pad, r.Height + 2 * pad);
                    gfx.DrawRectangle(XBrushes.White, rect);

                    var textPoint = new XPoint(r.X, dst.Height - r.Y - (r.Height * 0.2));
                    gfx.DrawString(replace, font, XBrushes.Black, textPoint);
                }
            }
        }

        output.Save(outPath);
        return outPath;
    }

    public async Task<string> RedactTextAsync(IFormFile file, string find, TextReplaceOptions? options)
    {
        if (string.IsNullOrWhiteSpace(find)) throw new ArgumentException("Suchtext darf nicht leer sein.");
        options ??= new TextReplaceOptions();

        var inPath = await SaveUpload(file);
        var outPath = TempPath("redact", ".pdf");

        var matches = FindTextOccurrences(inPath, find, options);

        using var input = PdfReader.Open(inPath, PdfDocumentOpenMode.Import);
        using var output = new SharpPdfDocument();

        for (int i = 0; i < input.PageCount; i++)
        {
            var src = input.Pages[i];
            var dst = output.AddPage(src);
            using var gfx = XGraphics.FromPdfPage(dst);

            if (matches.TryGetValue(i + 1, out var rects))
            {
                foreach (var r in rects)
                {
                    var pad = options.Padding;
                    var rect = new XRect(r.X - pad, dst.Height - (r.Y + r.Height) - pad, r.Width + 2 * pad, r.Height + 2 * pad);
                    gfx.DrawRectangle(XBrushes.Black, rect);
                }
            }
        }

        output.Save(outPath);
        return outPath;
    }

    // ========== Helpers ==========

    private async Task<string[]> SaveUploads(IFormFileCollection files)
    {
        var list = new List<string>();
        foreach (var f in files)
            list.Add(await SaveUpload(f));
        return list.ToArray();
    }

    private async Task<string> SaveUpload(IFormFile file)
    {
        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Nur PDF-Dateien erlaubt.");
        var path = TempPath(Path.GetFileNameWithoutExtension(file.FileName), ".pdf");
        using var fs = File.Create(path);
        await file.CopyToAsync(fs);
        return path;
    }

    private string TempPath(string prefix, string ext)
        => Path.Combine(_root, $"{prefix}-{Guid.NewGuid():N}{ext}");

    private static string? FindGhostscript()
    {
        if (OperatingSystem.IsWindows())
        {
            var candidates = new[] { "gswin64c.exe", "gswin32c.exe" };
            var envPaths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
            foreach (var p in envPaths)
            {
                foreach (var c in candidates)
                {
                    var cand = Path.Combine(p, c);
                    if (File.Exists(cand)) return cand;
                }
            }
        }
        if (File.Exists("/usr/bin/gs")) return "/usr/bin/gs";
        return null;
    }

    private static List<(int start, int end)> ParseRanges(string ranges, int pageCount)
    {
        var result = new List<(int, int)>();
        if (string.IsNullOrWhiteSpace(ranges))
            throw new ArgumentException("Bereiche dürfen nicht leer sein.");

        foreach (var token in ranges.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Contains('-', StringComparison.Ordinal))
            {
                var parts = token.Split('-', 2);
                int s = ParsePage(parts[0], pageCount);
                int e = ParsePage(parts[1], pageCount);
                if (s < 1 || e < 1 || s > e || s > pageCount) throw new ArgumentException($"Ungültiger Bereich: {token}");
                e = Math.Min(e, pageCount);
                result.Add((s, e));
            }
            else
            {
                int s = ParsePage(token, pageCount);
                if (s < 1 || s > pageCount) throw new ArgumentException($"Ungültige Seite: {token}");
                result.Add((s, s));
            }
        }
        return result;
    }

    private static int ParsePage(string s, int pageCount)
        => s.Trim().ToLower() switch
        {
            "end" or "ende" => pageCount,
            _ => int.TryParse(s, out var n) ? n : throw new ArgumentException($"Ungültiger Seitenwert: {s}")
        };

    private static HashSet<int> ParsePageList(string pages, int pageCount)
    {
        var set = new HashSet<int>();
        foreach (var (s, e) in ParseRanges(pages, pageCount))
            for (int i = s; i <= e; i++) set.Add(i);
        return set;
    }

    // ---------- Helper für Textsuche (PdfPig) ----------

    private record RectD(double X, double Y, double Width, double Height);

    private Dictionary<int, List<RectD>> FindTextOccurrences(string pdfPath, string term, TextReplaceOptions options)
    {
        var results = new Dictionary<int, List<RectD>>();

        // Vollqualifiziert, um Konflikte zu vermeiden:
        using var doc = UglyToad.PdfPig.PdfDocument.Open(pdfPath);

        for (int pageNum = 1; pageNum <= doc.NumberOfPages; pageNum++)
        {
            var page = doc.GetPage(pageNum);

            // Direkt aus PdfPig (kein DefaultWordExtractor nötig):
            var words = page.GetWords(); // IEnumerable<Word>

            if (words is null) continue;

            var termCmp = options.MatchCase ? term : term.ToLowerInvariant();

            foreach (var w in words)
            {
                var text = w.Text;
                var cmp = options.MatchCase ? text : text.ToLowerInvariant();

                bool isMatch = options.WholeWord ? (cmp == termCmp) : cmp.Contains(termCmp);
                if (!isMatch) continue;

                var b = w.BoundingBox; // (Left, Bottom, Width, Height)
                var rect = new RectD(b.Left, b.Bottom, b.Width, b.Height);

                if (!results.TryGetValue(pageNum, out var list))
                {
                    list = new List<RectD>();
                    results[pageNum] = list;
                }
                list.Add(rect);
            }
        }

        return results;
    }
}
