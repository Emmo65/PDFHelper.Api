using PdfHelper.Api.Services;
using PdfHelper.Api.Background;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddHostedService<CleanupService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles(); // wwwroot

// --- Endpoints ---
app.MapPost("/api/merge", async (HttpRequest req, IPdfService pdf) =>
{
    var form = await req.ReadFormAsync();
    var files = form.Files;
    if (files.Count < 2)
        return Results.BadRequest(new { errorCode = "MIN_FILES", message = "Mindestens 2 PDF-Dateien erforderlich." });

    var resultPath = await pdf.MergeAsync(files);
    return Results.File(resultPath, "application/pdf", "merged.pdf");
});

app.MapPost("/api/split", async (HttpRequest req, IPdfService pdf, string ranges) =>
{
    var form = await req.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file is null)
        return Results.BadRequest(new { errorCode = "FILE_MISSING", message = "PDF-Datei fehlt." });

    var zipPath = await pdf.SplitAsync(file, ranges);
    return Results.File(zipPath, "application/zip", "parts.zip");
});

app.MapPost("/api/rotate", async (HttpRequest req, IPdfService pdf, string pages, int angle) =>
{
    var form = await req.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file is null)
        return Results.BadRequest(new { errorCode = "FILE_MISSING", message = "PDF-Datei fehlt." });

    if (angle % 90 != 0) return Results.BadRequest(new { errorCode = "ANGLE_INVALID", message = "Winkel muss Vielfaches von 90 sein." });

    var resultPath = await pdf.RotateAsync(file, pages, angle);
    return Results.File(resultPath, "application/pdf", "rotated.pdf");
});

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

app.Run();
