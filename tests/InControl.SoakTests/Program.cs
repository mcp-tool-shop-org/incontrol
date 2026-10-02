using InControl.SoakTests;

Console.WriteLine("InControl Soak Test Runner");
Console.WriteLine("==========================");
Console.WriteLine();

// Parse arguments
var config = args.Length > 0 && args[0] == "--full"
    ? SoakTestConfig.Full
    : SoakTestConfig.Default;

if (args.Length > 0 && args[0] == "--quick")
{
    config = new SoakTestConfig
    {
        Duration = TimeSpan.FromMinutes(5),
        IterationDelay = TimeSpan.FromSeconds(1),
        MaxMemoryGrowthMB = 100
    };
}

Console.WriteLine($"Config: {(args.Length > 0 ? args[0] : "default")}");
Console.WriteLine($"  Duration: {config.Duration.TotalMinutes} minutes");
Console.WriteLine($"  Max Memory Growth: {config.MaxMemoryGrowthMB} MB");
Console.WriteLine();
Console.WriteLine("Press Ctrl+C to stop early...");
Console.WriteLine();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\nStopping soak test...");
};

var harness = new SoakTestHarness(config);
var report = await harness.RunAsync(cts.Token);

// Save report
var reportPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "InControl", "SoakTests",
    $"soak-report-{DateTime.Now:yyyyMMdd-HHmmss}.json");

Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
var json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
{
    WriteIndented = true
});
await File.WriteAllTextAsync(reportPath, json);

Console.WriteLine();
Console.WriteLine($"Report saved to: {reportPath}");

return report.Passed ? 0 : 1;
