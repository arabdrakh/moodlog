# MoodLog

MoodLog is a personal wellness tracker built with C#, .NET 10, and Blazor. It helps you record daily habits and review your own patterns over time.

The project is designed for local, single-user use. It is not a medical product and does not provide diagnoses or treatment advice.

## Features

- **Dashboard** — a daily summary of your mood, water intake, sleep, and activity.
- **Mood journal** — record, edit, or delete a mood; add optional tags and a short note.
- **Water tracker** — log, edit, and delete drinks; use the daily goal configured in Profile and see current progress.
- **Sleep tracker** — record, edit, and delete sleep sessions. MoodLog calculates the duration, including sleep across midnight.
- **Activity tracker** — record, edit, and delete the activity type, duration, and optional step count.
- **Analytics** — view daily charts and summaries for the last 7 or 30 days.
- **Smart Insights** — see simple observations based on the information you have logged.
- **CSV export** — download daily summaries for the selected period.
- **Profile and goals** — set your display name and personal water, sleep, and step goals; profile water goal consistently controls all summaries.

All records are stored in a local SQLite database. The application creates or updates the database automatically when it starts.


## How to run

1. Clone the https://github.com/arabdrakh/moodlog then open

2. Restore the NuGet packages:

   ```bash
   dotnet restore MoodLog.slnx
   ```

3. Start the web application with the HTTP launch profile:

   ```bash
   dotnet run --project src/MoodLog.Web/MoodLog.Web.csproj --launch-profile http
   ```

4. Open [http://localhost:5158](http://localhost:5158) in your browser.

Keep the terminal open while using the application. Press `Ctrl+C` in the terminal to stop it.

On the first start, MoodLog creates the SQLite database and applies its migrations. The database file is created relative to the web application's working directory unless a full database path is configured. Keep the `.db` file if you want to preserve your local records, and back it up before replacing the application.

## Run the tests

Automated tests help catch bugs when features change. They check validation rules, tracking services, analytics, and SQLite persistence and migrations. GitHub Actions runs the solution tests on Windows, Linux, and macOS. Browser end-to-end tests use Playwright to exercise mood persistence, profile settings, water CRUD and goal consistency, sleep/activity entry creation, analytics period selection, and CSV export. The current revision was verified on Windows with .NET SDK 10.0.403: Release build passed, all 55 unit/integration tests passed, and all 5 browser tests passed. This does not replace the cross-platform CI matrix or manual device checks.

From the project root, run:

```bash
dotnet test tests/MoodLog.Tests/MoodLog.Tests.csproj --collect:"XPlat Code Coverage"
```

On Windows, you can run `powershell.exe -ExecutionPolicy Bypass -File .\scripts\VERIFY_WINDOWS.ps1` from the extracted project root to execute restore, Release build, unit/integration tests, browser installation and E2E tests in order. It stops on any failed step. Verify the reported results rather than assuming success.

To run the browser end-to-end tests, build the test project and install Playwright's Chromium browser once. On Windows, use PowerShell:

```powershell
dotnet build tests/MoodLog.E2E/MoodLog.E2E.csproj
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\MoodLog.E2E\bin\Debug\net10.0\playwright.ps1 install chromium
dotnet test tests/MoodLog.E2E/MoodLog.E2E.csproj --no-build
```

On Linux or macOS, use PowerShell 7 (`pwsh`) for the browser install command:

```bash
dotnet build tests/MoodLog.E2E/MoodLog.E2E.csproj
pwsh tests/MoodLog.E2E/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test tests/MoodLog.E2E/MoodLog.E2E.csproj --no-build
```

The browser tests start the web app with a temporary SQLite database and remove that database when the test run finishes. They wait for Blazor interactivity and verify the downloaded CSV file contents. They require a working browser and web server.

## Check the app on a phone

For a quick responsive-layout check, open the browser's developer tools and switch to its mobile device mode. To open the app on a phone connected to the same trusted Wi-Fi network, run this command from Git Bash in the project root:

```bash
ASPNETCORE_URLS="http://0.0.0.0:5158" ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --project src/MoodLog.Web/MoodLog.Web.csproj
```

The development environment is needed for the app's local CSS assets when running with `--no-launch-profile`. Keep the terminal open, find the computer's local IP address, and open `http://<computer-ip>:5158` on the phone. Allow access through the firewall only on a trusted private network. Press `Ctrl+C` to stop the app after testing.


## Project structure

```text
src/
  MoodLog.Core/            Domain models, validation, and application services
  MoodLog.Infrastructure/  SQLite, Entity Framework Core, and repositories
  MoodLog.Web/             Blazor pages, components, and web application
tests/
  MoodLog.Tests/           xUnit unit and persistence tests
  MoodLog.E2E/             Playwright browser tests
MoodLog.slnx               .NET solution
```

The application separates core wellness logic from database access and the web interface:

- **Core** defines the wellness models, validation rules, repository interfaces, and services.
- **Infrastructure** implements the repositories with Entity Framework Core and SQLite.
- **Web** provides the interactive Blazor interface and connects the services to the pages.

## Data and privacy

MoodLog stores data in a SQLite file on the computer where the application runs. The current version does not include user accounts, cloud sync, or remote storage. Do not share the database file if it contains information you want to keep private.

Goals and Smart Insights are intended for personal tracking only. Insights are calculated from the data you enter and are not medical advice.

## Team Contributions

MoodLog was developed by two team members working together across the project:

- **Abdrakhmanova Aruzhan**
- **Nikita Zuy**

Both team members contributed jointly to planning, implementation, testing, and review. Work was shared across the core wellness features, the Blazor interface, SQLite persistence, analytics, and project quality.


## Screenshots

Desktop:

![alt text](screenshots/image.png)
![alt text](screenshots/image-1.png)
![alt text](screenshots/image-2.png)
![alt text](screenshots/image-3.png)
![alt text](screenshots/image-4.png)
![alt text](screenshots/image-5.png)
![alt text](screenshots/image-6.png)

Mobile phone:

![alt text](screenshots/image-7.png)
![alt text](screenshots/image-8.png)
![alt text](screenshots/image-9.png)
