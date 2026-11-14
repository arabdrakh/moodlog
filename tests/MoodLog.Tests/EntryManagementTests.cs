using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MoodLog.Core.Models;
using MoodLog.Core.Services;
using MoodLog.Infrastructure.Data;

namespace MoodLog.Tests;

public class EntryManagementTests
{
    [Fact]
    public async Task AllTrackers_ShouldUpdateAndDeleteSavedEntries()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MoodLogDbContext>()
            .UseSqlite(connection).Options;
        await using (var db = new MoodLogDbContext(options))
            await db.Database.EnsureCreatedAsync();

        var factory = new TestFactory(options);
        var changes = new WellnessState();
        var notifications = 0;
        changes.Changed += () => notifications++;
        var date = new DateOnly(2026, 11, 13);

        var moodRepository = new EfMoodEntryRepository(factory);
        var moods = new MoodJournalService(moodRepository, changes);
        var mood = (await moods.AddAsync(date, MoodLevel.Neutral, ["work"], "old")).Value;
        var updatedMood = await moods.UpdateAsync(mood.Id, date, MoodLevel.Good, ["home"], "new");
        Assert.True(updatedMood.IsSuccess);
        Assert.Equal(mood.Id, updatedMood.Value.Id);
        Assert.Equal(mood.CreatedAt, updatedMood.Value.CreatedAt);
        Assert.Equal("new", (await moodRepository.GetByIdAsync(mood.Id))!.Note);
        Assert.Equal(["home"], (await moodRepository.GetByIdAsync(mood.Id))!.Tags);
        Assert.True((await moods.DeleteAsync(mood.Id)).IsSuccess);
        Assert.Null(await moodRepository.GetByIdAsync(mood.Id));
        Assert.True((await moods.DeleteAsync(mood.Id)).IsFailure);

        var waterRepository = new EfWaterLogRepository(factory);
        var waters = new WaterTrackerService(waterRepository, changes);
        var water = (await waters.AddAsync(date, 250)).Value;
        var updatedWater = await waters.UpdateAsync(water.Id, date, 450, 3200);
        Assert.True(updatedWater.IsSuccess);
        Assert.Equal(water.LoggedAt, updatedWater.Value.LoggedAt);
        Assert.Equal(450, (await waterRepository.GetByIdAsync(water.Id))!.AmountMl);
        Assert.True((await waters.DeleteAsync(water.Id)).IsSuccess);
        Assert.Null(await waterRepository.GetByIdAsync(water.Id));

        var sleepRepository = new EfSleepLogRepository(factory);
        var sleeps = new SleepTrackerService(sleepRepository, changes);
        var sleep = (await sleeps.AddAsync(date, new TimeOnly(23, 0), new TimeOnly(7, 0))).Value;
        var updatedSleep = await sleeps.UpdateAsync(sleep.Id, date, new TimeOnly(22, 0), new TimeOnly(7, 0));
        Assert.True(updatedSleep.IsSuccess);
        Assert.Equal(540, (await sleepRepository.GetByIdAsync(sleep.Id))!.DurationMinutes);
        Assert.True((await sleeps.DeleteAsync(sleep.Id)).IsSuccess);
        Assert.Null(await sleepRepository.GetByIdAsync(sleep.Id));

        var activityRepository = new EfActivityLogRepository(factory);
        var activities = new ActivityTrackerService(activityRepository, changes);
        var activity = (await activities.AddAsync(date, ActivityType.Walking, 30, 2000)).Value;
        var updatedActivity = await activities.UpdateAsync(activity.Id, date, ActivityType.Running, 40, 4000);
        Assert.True(updatedActivity.IsSuccess);
        Assert.Equal(4000, (await activityRepository.GetByIdAsync(activity.Id))!.Steps);
        Assert.True((await activities.DeleteAsync(activity.Id)).IsSuccess);
        Assert.Null(await activityRepository.GetByIdAsync(activity.Id));

        Assert.Equal(12, notifications); // 3 successful operations per tracker
    }

    [Fact]
    public async Task InvalidUpdates_ShouldNotChangePersistedRecords()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MoodLogDbContext>()
            .UseSqlite(connection).Options;
        await using (var db = new MoodLogDbContext(options))
            await db.Database.EnsureCreatedAsync();
        var repo = new EfWaterLogRepository(new TestFactory(options));
        var tracker = new WaterTrackerService(repo);
        var date = new DateOnly(2026, 11, 13);
        var water = (await tracker.AddAsync(date, 250)).Value;

        Assert.True((await tracker.UpdateAsync(water.Id, date, -1)).IsFailure);
        Assert.Equal(250, (await repo.GetByIdAsync(water.Id))!.AmountMl);
        Assert.True((await tracker.UpdateAsync(Guid.NewGuid(), date, 500)).IsFailure);
        Assert.Equal(250, (await repo.GetByIdAsync(water.Id))!.AmountMl);
    }

    [Fact]
    public async Task ProfileWaterGoal_ShouldBeConsistentAcrossProgressAndAnalytics()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MoodLogDbContext>()
            .UseSqlite(connection).Options;
        await using (var db = new MoodLogDbContext(options))
            await db.Database.EnsureCreatedAsync();
        var factory = new TestFactory(options);
        var settingsRepository = new EfProfileSettingsRepository(factory);
        var waters = new EfWaterLogRepository(factory);
        var date = new DateOnly(2026, 11, 13);
        await waters.AddAsync(WaterLog.Create(date, 250, 2000).Value);
        await settingsRepository.SaveAsync(ProfileSettings.Create("Friend", 3200, 480, 8000).Value);

        var progress = await new WaterTrackerService(waters).GetDailyProgressAsync(date, 3200);
        var analytics = new WellnessAnalyticsService(
            new EfMoodEntryRepository(factory), waters,
            new EfSleepLogRepository(factory), new EfActivityLogRepository(factory),
            settingsRepository);
        var report = await analytics.GetReportAsync(date, 7);
        Assert.Equal(3200, progress.DailyGoalMl);
        Assert.Equal(3200, report.Days[^1].WaterGoalMl);
        Assert.Equal(3200, report.Days[0].WaterGoalMl); // also on days without entries
        Assert.Equal(250, report.Days[^1].WaterConsumedMl);
        // Old per-entry goal remains in storage, but the current profile goal drives reports.
        Assert.Equal(2000, (await waters.GetForDateAsync(date)).Single().DailyGoalMl);
    }

    private sealed class TestFactory(DbContextOptions<MoodLogDbContext> options)
        : IDbContextFactory<MoodLogDbContext>
    {
        public MoodLogDbContext CreateDbContext() => new(options);
        public ValueTask<MoodLogDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) => ValueTask.FromResult(CreateDbContext());
    }
}
