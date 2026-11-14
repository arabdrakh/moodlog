using MoodLog.Core.Interfaces;
using MoodLog.Core.Models;
using MoodLog.Core.Services;

namespace MoodLog.Tests;

public class WellnessAnalyticsTests
{
    [Fact]
    public async Task GetReportAsync_ShouldBuildCompleteDailySeriesAndAverages()
    {
        var repositories = new TestRepositories();
        var date = new DateOnly(2026, 11, 13);
        repositories.Moods.Add(MoodEntry.Create(date, MoodLevel.Good, null, null).Value);
        repositories.Water.Add(WaterLog.Create(date, 500, 2000).Value);
        repositories.Sleep.Add(SleepLog.Create(date, new TimeOnly(23, 0), new TimeOnly(7, 0)).Value);
        repositories.Activities.Add(ActivityLog.Create(date, ActivityType.Walking, 30, 2500).Value);
        var service = repositories.CreateService();

        var report = await service.GetReportAsync(date, 7);
        var trackedDay = report.Days.Single(day => day.Date == date);

        Assert.Equal(date.AddDays(-6), report.StartDate);
        Assert.Equal(7, report.Days.Count);
        Assert.Equal(4, trackedDay.AverageMood);
        Assert.Equal(500, trackedDay.WaterConsumedMl);
        Assert.Equal(2000, trackedDay.WaterGoalMl);
        Assert.Equal(480, trackedDay.SleepMinutes);
        Assert.True(trackedDay.HasActivity);
        Assert.Equal(30, trackedDay.ActivityMinutes);
        Assert.Equal(2500, trackedDay.Steps);
        Assert.Equal(4, report.AverageMood);
        Assert.Equal(500, report.AverageWaterMl);
        Assert.Equal(480, report.AverageSleepMinutes);
        Assert.Equal(30, report.TotalActivityMinutes);
    }

    [Fact]
    public async Task GetReportAsync_ShouldGenerateInsightsFromLoggedPatterns()
    {
        var repositories = new TestRepositories();
        var endDate = new DateOnly(2026, 11, 14);

        for (var offset = -13; offset <= 0; offset++)
        {
            var date = endDate.AddDays(offset);
            var sleepHours = offset < -6 ? 8 : 6;
            repositories.Sleep.Add(SleepLog.Create(
                date,
                new TimeOnly(23, 0),
                new TimeOnly(23, 0).AddHours(sleepHours)).Value);
            repositories.Activities.Add(ActivityLog.Create(
                date,
                ActivityType.Walking,
                offset < -6 ? 30 : 60,
                1000).Value);

            if (offset >= -4)
            {
                repositories.Water.Add(WaterLog.Create(date, 2000, 2000).Value);
            }

            if (offset is >= -3 and <= 0)
            {
                repositories.Moods.Add(MoodEntry.Create(date, MoodLevel.Good, null, null).Value);
            }
        }

        var report = await repositories.CreateService().GetReportAsync(endDate, 7);

        Assert.Contains(report.Insights, insight => insight.Contains("water goal for 5 days", StringComparison.Ordinal));
        Assert.Contains(report.Insights, insight => insight.Contains("sleep duration was lower", StringComparison.Ordinal));
        Assert.Contains(report.Insights, insight => insight.Contains("activity time increased", StringComparison.Ordinal));
        Assert.Contains(report.Insights, insight => insight.Contains("mood on 4 days", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetReportAsync_OneDayReportShouldLoadTwoWeeksForInsights()
    {
        var repositories = new TestRepositories();
        var endDate = new DateOnly(2026, 11, 14);

        for (var offset = -13; offset <= 0; offset++)
        {
            var date = endDate.AddDays(offset);
            var sleepHours = offset < -6 ? 8 : 6;
            repositories.Sleep.Add(SleepLog.Create(
                date,
                new TimeOnly(23, 0),
                new TimeOnly(23, 0).AddHours(sleepHours)).Value);
            repositories.Activities.Add(ActivityLog.Create(
                date,
                ActivityType.Walking,
                offset < -6 ? 30 : 60,
                1000).Value);
        }

        var report = await repositories.CreateService().GetReportAsync(endDate, 1);

        Assert.Single(report.Days);
        Assert.Contains(report.Insights, insight => insight.Contains("sleep duration was lower", StringComparison.Ordinal));
        Assert.Contains(report.Insights, insight => insight.Contains("activity time increased", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetReportAsync_ShouldRepresentUntrackedAveragesAsMissing()
    {
        var service = new TestRepositories().CreateService();

        var report = await service.GetReportAsync(new DateOnly(2026, 11, 14), 7);

        Assert.Null(report.AverageMood);
        Assert.Null(report.AverageWaterMl);
        Assert.Null(report.AverageSleepMinutes);
        Assert.Equal(0, report.TotalActivityMinutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    public async Task GetReportAsync_ShouldRejectInvalidPeriod(int days)
    {
        var service = new TestRepositories().CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetReportAsync(new DateOnly(2026, 11, 14), days));
    }

    private sealed class TestRepositories :
        IMoodEntryRepository,
        IWaterLogRepository,
        ISleepLogRepository,
        IActivityLogRepository
    {
        public List<MoodEntry> Moods { get; } = [];
        public List<WaterLog> Water { get; } = [];
        public List<SleepLog> Sleep { get; } = [];
        public List<ActivityLog> Activities { get; } = [];

        public WellnessAnalyticsService CreateService() =>
            new(this, this, this, this);

        Task<MoodEntry?> IMoodEntryRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<bool> IMoodEntryRepository.UpdateAsync(MoodEntry entry, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<bool> IMoodEntryRepository.DeleteAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task IMoodEntryRepository.AddAsync(MoodEntry entry, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<MoodEntry>> GetRecentAsync(
            int count,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MoodEntry>>(Moods.Take(count).ToArray());

        Task<IReadOnlyList<MoodEntry>> IMoodEntryRepository.GetForRangeAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MoodEntry>>(
                Moods.Where(entry => entry.Date >= startDate && entry.Date <= endDate).ToArray());

        Task<WaterLog?> IWaterLogRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<bool> IWaterLogRepository.UpdateAsync(WaterLog entry, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<bool> IWaterLogRepository.DeleteAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task IWaterLogRepository.AddAsync(WaterLog log, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<IReadOnlyList<WaterLog>> IWaterLogRepository.GetForDateAsync(
            DateOnly date,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WaterLog>>(Water.Where(log => log.Date == date).ToArray());

        Task<IReadOnlyList<WaterLog>> IWaterLogRepository.GetForRangeAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WaterLog>>(
                Water.Where(log => log.Date >= startDate && log.Date <= endDate).ToArray());

        Task<SleepLog?> ISleepLogRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<bool> ISleepLogRepository.UpdateAsync(SleepLog entry, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<bool> ISleepLogRepository.DeleteAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task ISleepLogRepository.AddAsync(SleepLog log, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<IReadOnlyList<SleepLog>> ISleepLogRepository.GetForDateAsync(
            DateOnly date,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SleepLog>>(Sleep.Where(log => log.Date == date).ToArray());

        Task<IReadOnlyList<SleepLog>> ISleepLogRepository.GetForRangeAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SleepLog>>(
                Sleep.Where(log => log.Date >= startDate && log.Date <= endDate).ToArray());

        Task<ActivityLog?> IActivityLogRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<bool> IActivityLogRepository.UpdateAsync(ActivityLog entry, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<bool> IActivityLogRepository.DeleteAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task IActivityLogRepository.AddAsync(ActivityLog log, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<IReadOnlyList<ActivityLog>> IActivityLogRepository.GetForDateAsync(
            DateOnly date,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ActivityLog>>(
                Activities.Where(log => log.Date == date).ToArray());

        Task<IReadOnlyList<ActivityLog>> IActivityLogRepository.GetForRangeAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ActivityLog>>(
                Activities.Where(log => log.Date >= startDate && log.Date <= endDate).ToArray());

    }
}
