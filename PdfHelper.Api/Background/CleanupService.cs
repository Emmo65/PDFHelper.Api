namespace PdfHelper.Api.Background;

public class CleanupService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var root = Path.Combine(Path.GetTempPath(), "pdfhelper");
        Directory.CreateDirectory(root);

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var age = DateTime.UtcNow - File.GetCreationTimeUtc(file);
                    if (age > TimeSpan.FromMinutes(2))
                        File.Delete(file);
                }
                catch { /* log optional */ }
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
