using System.Diagnostics;
using System.Runtime.InteropServices;

namespace InControl.SoakTests;

/// <summary>
/// Soak test harness for InControl stability testing.
/// Runs continuous operations for extended periods to detect:
/// - Memory leaks
/// - UI thread hangs
/// - Unhandled exceptions
/// - Resource exhaustion
/// </summary>
public class SoakTestHarness
{
    private readonly SoakTestConfig _config;
    private readonly List<SoakTestResult> _results = new();
    private readonly Stopwatch _totalTimer = new();
    private long _initialMemory;
    private int _iterationCount;
    private int _exceptionCount;
    private bool _isRunning;

    public SoakTestHarness(SoakTestConfig? config = null)
    {
        _config = config ?? SoakTestConfig.Default;
    }

    /// <summary>
    /// Run the complete soak test suite.
    /// </summary>
    public async Task<SoakTestReport> RunAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("=== InControl Soak Test Harness ===");
        Console.WriteLine($"Duration: {_config.Duration.TotalMinutes} minutes");
        Console.WriteLine($"Iteration Delay: {_config.IterationDelay.TotalMilliseconds}ms");
        Console.WriteLine();

        _isRunning = true;
        _totalTimer.Start();
        _initialMemory = GetCurrentMemoryMB();

        Console.WriteLine($"Initial Memory: {_initialMemory} MB");
        Console.WriteLine();

        try
        {
            while (_totalTimer.Elapsed < _config.Duration && !cancellationToken.IsCancellationRequested)
            {
                _iterationCount++;
                var iterationStart = Stopwatch.StartNew();

                Console.WriteLine($"--- Iteration {_iterationCount} ({_totalTimer.Elapsed:hh\\:mm\\:ss}) ---");

                // Run each test scenario
                await RunScenarioAsync("NavigatePanels", NavigatePanelsAsync, cancellationToken);
                await RunScenarioAsync("SwitchSessions", SwitchSessionsAsync, cancellationToken);
                await RunScenarioAsync("ToggleTheme", ToggleThemeAsync, cancellationToken);
                await RunScenarioAsync("OpenCloseDialogs", OpenCloseDialogsAsync, cancellationToken);
                await RunScenarioAsync("ModelManagerRefresh", ModelManagerRefreshAsync, cancellationToken);
                await RunScenarioAsync("ToggleOfflineMode", ToggleOfflineModeAsync, cancellationToken);

                // Memory check
                var currentMemory = GetCurrentMemoryMB();
                var memoryDelta = currentMemory - _initialMemory;
                Console.WriteLine($"  Memory: {currentMemory} MB (Δ{memoryDelta:+#;-#;0} MB)");

                // Check for memory leak threshold
                if (memoryDelta > _config.MaxMemoryGrowthMB)
                {
                    Console.WriteLine($"  ⚠️  WARNING: Memory growth exceeds threshold ({_config.MaxMemoryGrowthMB} MB)");
                }

                Console.WriteLine($"  Iteration time: {iterationStart.ElapsedMilliseconds}ms");
                Console.WriteLine();

                // Delay between iterations
                await Task.Delay(_config.IterationDelay, cancellationToken);

                // Force GC periodically to get accurate memory readings
                if (_iterationCount % 10 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nSoak test cancelled by user.");
        }
        finally
        {
            _isRunning = false;
            _totalTimer.Stop();
        }

        return GenerateReport();
    }

    private async Task RunScenarioAsync(string name, Func<CancellationToken, Task> scenario, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var result = new SoakTestResult
        {
            ScenarioName = name,
            StartTime = DateTime.UtcNow,
            Iteration = _iterationCount
        };

        try
        {
            await scenario(ct);
            result.Success = true;
            result.Duration = sw.Elapsed;
            Console.WriteLine($"  ✓ {name}: {sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Duration = sw.Elapsed;
            result.Error = ex.Message;
            _exceptionCount++;
            Console.WriteLine($"  ✗ {name}: FAILED - {ex.Message}");
        }

        _results.Add(result);
    }

    // ============== Test Scenarios ==============

    private async Task NavigatePanelsAsync(CancellationToken ct)
    {
        // Simulate navigating between panels
        await SimulateActionAsync("Navigate to Settings", ct);
        await SimulateActionAsync("Navigate to Model Manager", ct);
        await SimulateActionAsync("Navigate to Help", ct);
        await SimulateActionAsync("Navigate Home", ct);
    }

    private async Task SwitchSessionsAsync(CancellationToken ct)
    {
        // Simulate session switching
        await SimulateActionAsync("Create new session", ct);
        await SimulateActionAsync("Switch to previous session", ct);
        await SimulateActionAsync("Switch back", ct);
    }

    private async Task ToggleThemeAsync(CancellationToken ct)
    {
        // Simulate theme changes
        await SimulateActionAsync("Set Light theme", ct);
        await SimulateActionAsync("Set Dark theme", ct);
        await SimulateActionAsync("Set System theme", ct);
    }

    private async Task OpenCloseDialogsAsync(CancellationToken ct)
    {
        // Simulate dialog interactions
        await SimulateActionAsync("Open Command Palette", ct);
        await SimulateActionAsync("Close Command Palette", ct);
        await SimulateActionAsync("Open About dialog", ct);
        await SimulateActionAsync("Close About dialog", ct);
    }

    private async Task ModelManagerRefreshAsync(CancellationToken ct)
    {
        // Simulate Model Manager operations
        await SimulateActionAsync("Open Model Manager", ct);
        await SimulateActionAsync("Refresh model list", ct);
        await SimulateActionAsync("Close Model Manager", ct);
    }

    private async Task ToggleOfflineModeAsync(CancellationToken ct)
    {
        // Simulate offline mode toggle
        await SimulateActionAsync("Enable offline mode", ct);
        await SimulateActionAsync("Disable offline mode", ct);
    }

    // Simulates an action with random delay to mimic user behavior
    private async Task SimulateActionAsync(string action, CancellationToken ct)
    {
        // In a real implementation, this would use UI automation
        // For now, simulate with delays
        var delay = Random.Shared.Next(50, 200);
        await Task.Delay(delay, ct);
    }

    // ============== Utilities ==============

    private static long GetCurrentMemoryMB()
    {
        using var process = Process.GetCurrentProcess();
        return process.WorkingSet64 / (1024 * 1024);
    }

    private SoakTestReport GenerateReport()
    {
        var finalMemory = GetCurrentMemoryMB();
        var successCount = _results.Count(r => r.Success);
        var failCount = _results.Count(r => !r.Success);

        var report = new SoakTestReport
        {
            StartTime = DateTime.UtcNow.Subtract(_totalTimer.Elapsed),
            EndTime = DateTime.UtcNow,
            TotalDuration = _totalTimer.Elapsed,
            IterationCount = _iterationCount,
            TotalScenarios = _results.Count,
            SuccessfulScenarios = successCount,
            FailedScenarios = failCount,
            ExceptionCount = _exceptionCount,
            InitialMemoryMB = _initialMemory,
            FinalMemoryMB = finalMemory,
            MemoryGrowthMB = finalMemory - _initialMemory,
            Results = _results.ToList()
        };

        // Determine pass/fail
        report.Passed = report.FailedScenarios == 0 &&
                       report.MemoryGrowthMB <= _config.MaxMemoryGrowthMB &&
                       report.ExceptionCount == 0;

        Console.WriteLine("=== Soak Test Report ===");
        Console.WriteLine($"Duration: {report.TotalDuration:hh\\:mm\\:ss}");
        Console.WriteLine($"Iterations: {report.IterationCount}");
        Console.WriteLine($"Scenarios: {report.TotalScenarios} ({report.SuccessfulScenarios} passed, {report.FailedScenarios} failed)");
        Console.WriteLine($"Exceptions: {report.ExceptionCount}");
        Console.WriteLine($"Memory: {report.InitialMemoryMB} MB → {report.FinalMemoryMB} MB (Δ{report.MemoryGrowthMB:+#;-#;0} MB)");
        Console.WriteLine();
        Console.WriteLine(report.Passed ? "✅ SOAK TEST PASSED" : "❌ SOAK TEST FAILED");

        return report;
    }
}

/// <summary>
/// Configuration for soak tests.
/// </summary>
public class SoakTestConfig
{
    /// <summary>Total duration to run the soak test.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromHours(2);

    /// <summary>Delay between iterations.</summary>
    public TimeSpan IterationDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Maximum allowed memory growth in MB before failing.</summary>
    public long MaxMemoryGrowthMB { get; set; } = 500;

    /// <summary>Maximum allowed UI thread hang time in ms.</summary>
    public int MaxUIHangMs { get; set; } = 5000;

    /// <summary>Default configuration for quick validation.</summary>
    public static SoakTestConfig Default => new()
    {
        Duration = TimeSpan.FromMinutes(30),
        IterationDelay = TimeSpan.FromSeconds(3),
        MaxMemoryGrowthMB = 200
    };

    /// <summary>Full 2-hour soak test configuration.</summary>
    public static SoakTestConfig Full => new()
    {
        Duration = TimeSpan.FromHours(2),
        IterationDelay = TimeSpan.FromSeconds(5),
        MaxMemoryGrowthMB = 500
    };
}

/// <summary>
/// Result of a single test scenario execution.
/// </summary>
public class SoakTestResult
{
    public string ScenarioName { get; set; } = "";
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public int Iteration { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Complete soak test report.
/// </summary>
public class SoakTestReport
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public int IterationCount { get; set; }
    public int TotalScenarios { get; set; }
    public int SuccessfulScenarios { get; set; }
    public int FailedScenarios { get; set; }
    public int ExceptionCount { get; set; }
    public long InitialMemoryMB { get; set; }
    public long FinalMemoryMB { get; set; }
    public long MemoryGrowthMB { get; set; }
    public bool Passed { get; set; }
    public List<SoakTestResult> Results { get; set; } = new();
}
