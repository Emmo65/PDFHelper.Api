using Microsoft.AspNetCore.Http;
using PdfHelper.Api.Background;
using PdfHelper.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddHostedService<CleanupService>();

var app = builder.Build();

// Static files (wwwroot/index.html wird automatisch als Startseite verwendet)
app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

// -------------------- PDF Kernfunktionen --------------------

// Merge
app.MapPost("/api/merge", async (HttpRequest req, IPdfService pdf) =>
{
    var form = await req.ReadFormAsync();
    var files = form.Files;
    if (files.Count < 2)
        return Results.BadRequest(new { errorCode = "MIN_FILES", message = "Mindestens 2 PDF-Dateien erforderlich." });

    var resultPath = await pdf.MergeAsync(files);
    return Results.File(resultPath, "application/pdf", "merged.pdf");
});

// Split
app.MapPost("/api/split", async (HttpRequest req, IPdfService pdf, string ranges) =>
{
    var form = await req.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file is null)
        return Results.BadRequest(new { errorCode = "FILE_MISSING", message = "PDF-Datei fehlt." });

    try
    {
        var zipPath = await pdf.SplitAsync(file, ranges);
        return Results.File(zipPath, "application/zip", "parts.zip");
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errorCode = "INVALID_RANGES", message = ex.Message });
    }
});

// Rotate
app.MapPost("/api/rotate", async (HttpRequest req, IPdfService pdf, string pages, int angle) =>
{
    var form = await req.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file is null)
        return Results.BadRequest(new { errorCode = "FILE_MISSING", message = "PDF-Datei fehlt." });

    if (angle % 90 != 0)
        return Results.BadRequest(new { errorCode = "ANGLE_INVALID", message = "Winkel muss Vielfaches von 90 sein (90/180/270)." });

    try
    {
        var resultPath = await pdf.RotateAsync(file, pages, angle);
        return Results.File(resultPath, "application/pdf", "rotated.pdf");
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errorCode = "INVALID_PAGES", message = ex.Message });
    }
});

// Compress
app.MapPost("/api/compress", async (HttpRequest req, IPdfService pdf, string level = "standard") =>
{
    var form = await req.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file is null)
        return Results.BadRequest(new { errorCode = "FILE_MISSING", message = "PDF-Datei fehlt." });

    var result = await pdf.CompressAsync(file, level);
    if (!result.Success)
        return Results.BadRequest(new { errorCode = result.ErrorCode, message = result.Message });

    return Results.File(result.OutputPath!, "application/pdf", "compressed.pdf");
});

// -------------------- Text-Bearbeitung --------------------

// Text ersetzen (Overlay)
app.MapPost("/api/text/replace", async (HttpRequest req, IPdfService pdf, string find, string replace, bool matchCase = false, bool wholeWord = true) =>
{
    if (string.IsNullOrWhiteSpace(find))
        return Results.BadRequest(new { errorCode = "INVALID_INPUT", message = "Suchtext darf nicht leer sein." });

    var form = await req.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file is null)
        return Results.BadRequest(new { errorCode = "FILE_MISSING", message = "PDF-Datei fehlt." });

    var options = new TextReplaceOptions(MatchCase: matchCase, WholeWord: wholeWord);
    try
    {
        var outPath = await pdf.ReplaceTextAsync(file, find, replace, options);
        return Results.File(outPath, "application/pdf", "text-replaced.pdf");
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errorCode = "INVALID_INPUT", message = ex.Message });
    }
});

// Text schwärzen (Redact)
app.MapPost("/api/text/redact", async (HttpRequest req, IPdfService pdf, string find, bool matchCase = false, bool wholeWord = true) =>
{
    if (string.IsNullOrWhiteSpace(find))
        return Results.BadRequest(new { errorCode = "INVALID_INPUT", message = "Suchtext darf nicht leer sein." });

    var form = await req.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file is null)
        return Results.BadRequest(new { errorCode = "FILE_MISSING", message = "PDF-Datei fehlt." });

    var options = new TextReplaceOptions(MatchCase: matchCase, WholeWord: wholeWord);
    try
    {
        var outPath = await pdf.RedactTextAsync(file, find, options);
        return Results.File(outPath, "application/pdf", "text-redacted.pdf");
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errorCode = "INVALID_INPUT", message = ex.Message });
    }
});

app.Run();
