using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace MoodLog.E2E;

public sealed class MoodLogBrowserTests(MoodLogWebFixture fixture) : IClassFixture<MoodLogWebFixture>
{
    [Fact]
    public async Task MoodCheckIn_ShouldSaveAndRemainVisibleAfterReload()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions { AcceptDownloads = true });
        var page = await context.NewPageAsync();
        await NavigateAndWaitForInteractiveAsync(page, $"{fixture.BaseUrl}/mood");

        await page.Locator("#entry-level").SelectOptionAsync("Good");
        await page.Locator("#entry-tags").FillAsync("browser, e2e");
        await page.Locator("#entry-note").FillAsync($"Saved by browser test {Guid.NewGuid():N}");
        var note = await page.Locator("#entry-note").InputValueAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Save check-in" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Status))
            .ToHaveTextAsync("Your check-in has been saved.");
        await Assertions.Expect(page.Locator(".mood-entry"))
            .ToContainTextAsync(note);
        await Assertions.Expect(page.Locator(".mood-tags"))
            .ToContainTextAsync("browser");

        await page.ReloadAsync();

        await Assertions.Expect(page.Locator(".mood-entry"))
            .ToContainTextAsync(note);

        await page.GetByRole(AriaRole.Button, new() { Name = "Edit mood entry" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Save changes" }))
            .ToBeVisibleAsync();
        await page.Locator("#entry-note").FillAsync("Updated browser check-in");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save changes" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Status))
            .ToHaveTextAsync("Your check-in has been updated.");
        await page.ReloadAsync();
        await NavigateAndWaitForInteractiveAsync(page, $"{fixture.BaseUrl}/mood");
        await Assertions.Expect(page.Locator(".mood-entry"))
            .ToContainTextAsync("Updated browser check-in");

        page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Delete mood entry" }).ClickAsync();
        await Assertions.Expect(page.Locator(".mood-entry")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task ProfileSettings_ShouldUpdateDashboardGreeting()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions { AcceptDownloads = true });
        var page = await context.NewPageAsync();
        var displayName = $"E2E-{Guid.NewGuid():N}";

        await NavigateAndWaitForInteractiveAsync(page, $"{fixture.BaseUrl}/profile");
        await page.Locator("#profile-name").FillAsync(displayName);
        await page.GetByRole(AriaRole.Button, new() { Name = "Save settings" }).ClickAsync();

        var status = page.GetByRole(AriaRole.Status);
        await Assertions.Expect(status)
            .ToHaveTextAsync("Your profile and goals have been saved.");

        await NavigateAndWaitForInteractiveAsync(page, fixture.BaseUrl);
        await Assertions.Expect(page.Locator("h1"))
            .ToContainTextAsync(displayName);
    }

    [Fact]
    public async Task WaterEntry_ShouldEditDeleteAndUseUpdatedProfileGoal()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions { AcceptDownloads = true });
        var page = await context.NewPageAsync();
        await NavigateAndWaitForInteractiveAsync(page, $"{fixture.BaseUrl}/profile");
        await page.Locator("#profile-water-goal").FillAsync("3200");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save settings" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Status))
            .ToHaveTextAsync("Your profile and goals have been saved.");

        await NavigateAndWaitForInteractiveAsync(page, $"{fixture.BaseUrl}/water");
        await Assertions.Expect(page.Locator(".water-goal")).ToContainTextAsync("3200 ml");
        await page.Locator("#water-amount").FillAsync("250");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add to daily total" }).ClickAsync();
        await Assertions.Expect(page.Locator(".water-total strong")).ToHaveTextAsync("250");

        await page.GetByRole(AriaRole.Button, new() { Name = "Edit water entry" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Save changes" }))
            .ToBeVisibleAsync();
        await page.Locator("#water-amount").FillAsync("400");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save changes" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Status))
            .ToHaveTextAsync("Water entry updated.");
        await page.ReloadAsync();
        await NavigateAndWaitForInteractiveAsync(page, $"{fixture.BaseUrl}/water");
        await Assertions.Expect(page.Locator(".water-total strong")).ToHaveTextAsync("400");

        // Regression: changing the goal AFTER logging must update every screen.
        await NavigateAndWaitForInteractiveAsync(page, $"{fixture.BaseUrl}/profile");
        await page.Locator("#profile-water-goal").FillAsync("4200");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save settings" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Status))
            .ToHaveTextAsync("Your profile and goals have been saved.");
        await NavigateAndWaitForInteractiveAsync(page, $"{fixture.BaseUrl}/water");
        await Assertions.Expect(page.Locator(".water-goal")).ToContainTextAsync("4200 ml");
        await Assertions.Expect(page.Locator(".water-total strong")).ToHaveTextAsync("400");
        await NavigateAndWaitForInteractiveAsync(page, $"{fixture.BaseUrl}/analytics");
        await Assertions.Expect(page.Locator(".analytics-stat").Nth(1)).ToContainTextAsync("400 ml");
        await NavigateAndWaitForInteractiveAsync(page, $"{fixture.BaseUrl}/water");

        page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Delete water entry" }).ClickAsync();
        await Assertions.Expect(page.Locator(".water-log-list li")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator(".water-total strong")).ToHaveTextAsync("0");
    }

    [Fact]
    public async Task SleepAndActivity_ShouldSaveAndDisplayHistory()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions { AcceptDownloads = true });
        var page = await context.NewPageAsync();
        await NavigateAndWaitForInteractiveAsync(page, $"{fixture.BaseUrl}/sleep");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save sleep" }).ClickAsync();
        await Assertions.Expect(page.Locator(".tracker-list li")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator(".tracker-list li")).ToContainTextAsync("8h 30m");
        await page.GetByRole(AriaRole.Button, new() { Name = "Edit sleep entry" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Save changes" }))
            .ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Save changes" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToHaveTextAsync("Sleep entry updated.");
        page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Delete sleep entry" }).ClickAsync();
        await Assertions.Expect(page.Locator(".tracker-list li")).ToHaveCountAsync(0);

        await NavigateAndWaitForInteractiveAsync(page, $"{fixture.BaseUrl}/activity");
        await page.Locator("#activity-duration").FillAsync("40");
        await page.Locator("#activity-steps").FillAsync("2500");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save activity" }).ClickAsync();
        await Assertions.Expect(page.Locator(".tracker-list li")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator(".activity-summary"))
            .ToContainTextAsync(new Regex(@"2(?:[\s,.\u00A0\u202F])?500"));
        await page.GetByRole(AriaRole.Button, new() { Name = "Edit activity entry" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Save changes" }))
            .ToBeVisibleAsync();
        await page.Locator("#activity-duration").FillAsync("50");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save changes" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToHaveTextAsync("Activity updated.");
        await Assertions.Expect(page.Locator(".activity-summary")).ToContainTextAsync("50");
        await page.GetByRole(AriaRole.Button, new() { Name = "Delete activity entry" }).ClickAsync();
        await Assertions.Expect(page.Locator(".tracker-list li")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Analytics_ShouldSwitchPeriodAndExportCsv()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions { AcceptDownloads = true });
        var page = await context.NewPageAsync();
        await NavigateAndWaitForInteractiveAsync(page, $"{fixture.BaseUrl}/analytics");
        await page.Locator("#analytics-period").SelectOptionAsync("30");
        await Assertions.Expect(page.Locator(".chart-column").First)
            .ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".chart-card").First.Locator(".chart-column"))
            .ToHaveCountAsync(30);

        var download = await page.RunAndWaitForDownloadAsync(async () =>
            await page.GetByRole(AriaRole.Button, new() { Name = "Download CSV" }).ClickAsync());
        Assert.EndsWith(".csv", download.SuggestedFilename);
        var csvPath = Path.Combine(Path.GetTempPath(), $"moodlog-{Guid.NewGuid():N}.csv");
        try
        {
            await download.SaveAsAsync(csvPath);
            var csv = await File.ReadAllTextAsync(csvPath);
            var rows = csv.TrimStart('\uFEFF').Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(31, rows.Length); // header + 30 daily rows
            Assert.StartsWith("Date,Mood (1-5),Water (ml),Water goal (ml)", rows[0]);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
        await Assertions.Expect(page.GetByRole(AriaRole.Status))
            .ToHaveTextAsync("Your report is ready to download.");
    }

    private static async Task NavigateAndWaitForInteractiveAsync(IPage page, string url)
    {
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Assertions.Expect(page.Locator(".page[data-interactive='true']"))
            .ToBeVisibleAsync();
    }
}

public sealed class MoodLogWebFixture : IAsyncLifetime
{
    private readonly string _databaseDirectory = Path.Combine(
        Path.GetTempPath(),
        $"moodlog-e2e-{Guid.NewGuid():N}");
    private readonly StringBuilder _serverOutput = new();
    private Process? _server;
    private IPlaywright? _playwright;

    public string BaseUrl { get; private set; } = string.Empty;
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_databaseDirectory);
        var port = GetAvailablePort();
        BaseUrl = $"http://127.0.0.1:{port}";

        var repositoryRoot = FindRepositoryRoot();
        var webProject = Path.Combine(
            repositoryRoot,
            "src",
            "MoodLog.Web",
            "MoodLog.Web.csproj");
        var testConfiguration = new DirectoryInfo(AppContext.BaseDirectory)
            .Parent?.Name
            ?? throw new InvalidOperationException("Could not determine the E2E test configuration.");
        var processInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        processInfo.ArgumentList.Add("run");
        processInfo.ArgumentList.Add("--configuration");
        processInfo.ArgumentList.Add(testConfiguration);
        processInfo.ArgumentList.Add("--no-build");
        processInfo.ArgumentList.Add("--no-launch-profile");
        processInfo.ArgumentList.Add("--project");
        processInfo.ArgumentList.Add(webProject);
        processInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        processInfo.Environment["ASPNETCORE_URLS"] = BaseUrl;
        processInfo.Environment["ConnectionStrings__MoodLog"] =
            $"Data Source={Path.Combine(_databaseDirectory, "moodlog.db")}";

        _server = new Process { StartInfo = processInfo, EnableRaisingEvents = true };
        _server.OutputDataReceived += CaptureServerOutput;
        _server.ErrorDataReceived += CaptureServerOutput;
        if (!_server.Start())
        {
            throw new InvalidOperationException("The MoodLog web application could not be started.");
        }

        _server.BeginOutputReadLine();
        _server.BeginErrorReadLine();
        await WaitForServerAsync();

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }

        _playwright?.Dispose();

        if (_server is { HasExited: false })
        {
            _server.Kill(entireProcessTree: true);
            await _server.WaitForExitAsync();
        }

        _server?.Dispose();

        if (Directory.Exists(_databaseDirectory))
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    Directory.Delete(_databaseDirectory, recursive: true);
                    break;
                }
                catch (IOException) when (attempt < 5)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1)));
                }
            }
        }
    }

    private async Task WaitForServerAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(45))
        {
            if (_server is { HasExited: true })
            {
                throw new InvalidOperationException(
                    $"The MoodLog web application exited before becoming ready.{Environment.NewLine}{_serverOutput}");
            }

            try
            {
                using var response = await client.GetAsync(BaseUrl);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception exception) when (
                exception is HttpRequestException or TaskCanceledException)
            {
            }

            await Task.Delay(250);
        }

        throw new TimeoutException(
            $"The MoodLog web application did not become ready in time.{Environment.NewLine}{_serverOutput}");
    }

    private void CaptureServerOutput(object sender, DataReceivedEventArgs args)
    {
        if (args.Data is null)
        {
            return;
        }

        lock (_serverOutput)
        {
            _serverOutput.AppendLine(args.Data);
        }
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MoodLog.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate MoodLog.slnx from the browser-test output directory.");
    }
}
