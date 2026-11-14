using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MoodLog.Core.Interfaces;
using MoodLog.Core.Models;
using MoodLog.Infrastructure.Data;

namespace MoodLog.Tests;

public class SqlitePersistenceTests
{
    [Fact]
    public async Task DatabaseInitializer_ShouldCreateFullSchemaForNewDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MoodLogDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new MoodLogDbContext(options);
        await DatabaseInitializer.InitializeAsync(context);

        var migrations = await context.Database.GetAppliedMigrationsAsync();
        Assert.Equal(3, migrations.Count());
        Assert.Equal(0, await context.Profiles.CountAsync());
        Assert.Empty(await context.SleepLogs.ToListAsync());
        Assert.Empty(await context.ActivityLogs.ToListAsync());
    }

    [Fact]
    public async Task DatabaseInitializer_ShouldUpgradeExistingMoodAndWaterDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                CREATE TABLE "MoodEntries" (
                    "Id" TEXT NOT NULL PRIMARY KEY,
                    "Date" TEXT NOT NULL,
                    "Level" INTEGER NOT NULL,
                    "Tags" TEXT NOT NULL,
                    "Note" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL
                );
                CREATE TABLE "WaterLogs" (
                    "Id" TEXT NOT NULL PRIMARY KEY,
                    "Date" TEXT NOT NULL,
                    "AmountMl" INTEGER NOT NULL,
                    "DailyGoalMl" INTEGER NOT NULL,
                    "LoggedAt" TEXT NOT NULL
                );
                INSERT INTO "MoodEntries" ("Id", "Date", "Level", "Tags", "Note", "CreatedAt")
                VALUES ('11111111-1111-1111-1111-111111111111', '2026-11-13', 4, '["work"]', 'Saved before upgrade', '2026-11-13 12:00:00');
                INSERT INTO "WaterLogs" ("Id", "Date", "AmountMl", "DailyGoalMl", "LoggedAt")
                VALUES ('22222222-2222-2222-2222-222222222222', '2026-11-13', 250, 2000, '2026-11-13 12:00:00');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<MoodLogDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new MoodLogDbContext(options);

        await DatabaseInitializer.InitializeAsync(context);

        var migrations = await context.Database.GetAppliedMigrationsAsync();
        Assert.Equal(3, migrations.Count());
        Assert.Empty(await context.SleepLogs.ToListAsync());
        Assert.Empty(await context.ActivityLogs.ToListAsync());
        Assert.Empty(await context.Profiles.ToListAsync());
        Assert.Equal("Saved before upgrade", (await context.MoodEntries.SingleAsync()).Note);
        Assert.Equal(250, (await context.WaterLogs.SingleAsync()).AmountMl);
    }

    [Fact]
    public async Task GetRecentAsync_ShouldReturnNewestEntriesFirstAndApplyLimit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MoodLogDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new MoodLogDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var olderEntry = MoodEntry.Create(
            new DateOnly(2026, 11, 12),
            MoodLevel.Neutral,
            null,
            null).Value;
        var newerEntry = MoodEntry.Create(
            new DateOnly(2026, 11, 13),
            MoodLevel.Good,
            null,
            null).Value;
        var repository = new EfMoodEntryRepository(new TestDbContextFactory(options));

        await repository.AddAsync(olderEntry);
        await repository.AddAsync(newerEntry);
        var recentEntries = await repository.GetRecentAsync(1);

        Assert.Equal(newerEntry.Id, Assert.Single(recentEntries).Id);
    }

    [Fact]
    public async Task SaveChanges_ShouldPersistMoodAndWaterLogs()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MoodLogDbContext>()
            .UseSqlite(connection)
            .Options;

        var moodEntry = MoodEntry.Create(
            new DateOnly(2026, 11, 13),
            MoodLevel.Good,
            ["work", "rest"],
            "Felt balanced").Value;
        var waterLog = WaterLog.Create(
            new DateOnly(2026, 11, 13),
            300,
            2200).Value;

        await using (var context = new MoodLogDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.MoodEntries.Add(moodEntry);
            context.WaterLogs.Add(waterLog);
            await context.SaveChangesAsync();
        }

        await using (var context = new MoodLogDbContext(options))
        {
            var savedMoodEntry = await context.MoodEntries.SingleAsync();
            var savedWaterLog = await context.WaterLogs.SingleAsync();

            Assert.Equal(moodEntry.Id, savedMoodEntry.Id);
            Assert.Equal(["work", "rest"], savedMoodEntry.Tags);
            Assert.Equal(moodEntry.Note, savedMoodEntry.Note);
            Assert.Equal(waterLog.Id, savedWaterLog.Id);
            Assert.Equal(300, savedWaterLog.AmountMl);
            Assert.Equal(2200, savedWaterLog.DailyGoalMl);
        }
    }

    [Fact]
    public async Task GetForDateAsync_ShouldReturnOnlyLogsForRequestedDate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MoodLogDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new MoodLogDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var requestedDate = new DateOnly(2026, 11, 13);
        var requestedLog = WaterLog.Create(requestedDate, 300).Value;
        var otherDayLog = WaterLog.Create(requestedDate.AddDays(-1), 500).Value;
        var repository = new EfWaterLogRepository(new TestDbContextFactory(options));

        await repository.AddAsync(otherDayLog);
        await repository.AddAsync(requestedLog);
        var logs = await repository.GetForDateAsync(requestedDate);

        Assert.Equal(requestedLog.Id, Assert.Single(logs).Id);
    }

    [Fact]
    public async Task SaveChanges_ShouldPersistSleepAndActivityLogs()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MoodLogDbContext>()
            .UseSqlite(connection)
            .Options;

        var date = new DateOnly(2026, 11, 13);
        var sleepLog = SleepLog.Create(date, new TimeOnly(23, 0), new TimeOnly(7, 0)).Value;
        var activityLog = ActivityLog.Create(date, ActivityType.Walking, 35, 4200).Value;

        await using (var context = new MoodLogDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
        }

        var contextFactory = new TestDbContextFactory(options);
        await new EfSleepLogRepository(contextFactory).AddAsync(sleepLog);
        await new EfActivityLogRepository(contextFactory).AddAsync(activityLog);

        await using (var context = new MoodLogDbContext(options))
        {
            var savedSleep = await context.SleepLogs.SingleAsync();
            var savedActivity = await context.ActivityLogs.SingleAsync();

            Assert.Equal(8 * 60, savedSleep.DurationMinutes);
            Assert.Equal(ActivityType.Walking, savedActivity.Type);
            Assert.Equal(4200, savedActivity.Steps);
        }
    }

    [Fact]
    public async Task ProfileSettings_ShouldUpdateExistingLocalProfile()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MoodLogDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new MoodLogDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var repository = new EfProfileSettingsRepository(new TestDbContextFactory(options));
        var initial = ProfileSettings.Create("Alex", 2200, 480, 8000).Value;
        var updated = ProfileSettings.Create("Jordan", 2600, 510, 10000).Value;

        await repository.SaveAsync(initial);
        await repository.SaveAsync(updated);
        var saved = await repository.GetAsync();

        Assert.NotNull(saved);
        Assert.Equal("Jordan", saved.DisplayName);
        Assert.Equal(2600, saved.DailyWaterGoalMl);
        Assert.Equal(510, saved.DailySleepGoalMinutes);
        Assert.Equal(10000, saved.DailyStepGoal);
        Assert.Equal(1, await context.Profiles.CountAsync());
    }

    private sealed class TestDbContextFactory(DbContextOptions<MoodLogDbContext> options)
        : IDbContextFactory<MoodLogDbContext>
    {
        public MoodLogDbContext CreateDbContext() => new(options);

        public ValueTask<MoodLogDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CreateDbContext());
    }
}
