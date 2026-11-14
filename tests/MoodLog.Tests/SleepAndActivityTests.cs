using MoodLog.Core.Interfaces;
using MoodLog.Core.Models;
using MoodLog.Core.Services;

namespace MoodLog.Tests;

public class SleepAndActivityTests
{
    [Fact]
    public void SleepLogCreate_ShouldCalculateDurationAcrossMidnight()
    {
        var result = SleepLog.Create(
            new DateOnly(2026, 11, 13),
            new TimeOnly(22, 30),
            new TimeOnly(6, 30));

        Assert.True(result.IsSuccess);
        Assert.Equal(8 * 60, result.Value.DurationMinutes);
    }

    [Fact]
    public void SleepLogCreate_ShouldRejectDurationOver16Hours()
    {
        var result = SleepLog.Create(
            new DateOnly(2026, 11, 13),
            new TimeOnly(7, 0),
            new TimeOnly(23, 1));

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1441, 0)]
    [InlineData(30, -1)]
    [InlineData(30, 100001)]
    public void ActivityLogCreate_ShouldRejectInvalidValues(int durationMinutes, int steps)
    {
        var result = ActivityLog.Create(
            new DateOnly(2026, 11, 13),
            ActivityType.Walking,
            durationMinutes,
            steps);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TrackerServices_ShouldPersistValidEntriesOnly()
    {
        var sleepRepository = new TestSleepLogRepository();
        var activityRepository = new TestActivityLogRepository();
        var sleepService = new SleepTrackerService(sleepRepository);
        var activityService = new ActivityTrackerService(activityRepository);
        var date = new DateOnly(2026, 11, 13);

        var sleepResult = await sleepService.AddAsync(date, new TimeOnly(23, 0), new TimeOnly(7, 0));
        var activityResult = await activityService.AddAsync(date, ActivityType.Walking, 30, 3500);
        var invalidActivity = await activityService.AddAsync(date, ActivityType.Running, 0, 0);

        Assert.True(sleepResult.IsSuccess);
        Assert.True(activityResult.IsSuccess);
        Assert.True(invalidActivity.IsFailure);
        Assert.Single(sleepRepository.Logs);
        Assert.Single(activityRepository.Logs);
    }

    private sealed class TestSleepLogRepository : ISleepLogRepository
    {
        public Task<SleepLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Logs.FirstOrDefault(item => item.Id == id));

        public Task<bool> UpdateAsync(SleepLog item, CancellationToken cancellationToken = default)
        {
            var index = Logs.FindIndex(old => old.Id == item.Id);
            if (index == -1) return Task.FromResult(false);
            Logs[index] = item;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Logs.RemoveAll(item => item.Id == id) > 0);

        public List<SleepLog> Logs { get; } = [];

        public Task AddAsync(SleepLog log, CancellationToken cancellationToken = default)
        {
            Logs.Add(log);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SleepLog>> GetForDateAsync(
            DateOnly date,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SleepLog>>(Logs.Where(log => log.Date == date).ToArray());

        public Task<IReadOnlyList<SleepLog>> GetForRangeAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SleepLog>>(
                Logs.Where(log => log.Date >= startDate && log.Date <= endDate).ToArray());
    }

    private sealed class TestActivityLogRepository : IActivityLogRepository
    {
        public Task<ActivityLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Logs.FirstOrDefault(item => item.Id == id));

        public Task<bool> UpdateAsync(ActivityLog item, CancellationToken cancellationToken = default)
        {
            var index = Logs.FindIndex(old => old.Id == item.Id);
            if (index == -1) return Task.FromResult(false);
            Logs[index] = item;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Logs.RemoveAll(item => item.Id == id) > 0);

        public List<ActivityLog> Logs { get; } = [];

        public Task AddAsync(ActivityLog log, CancellationToken cancellationToken = default)
        {
            Logs.Add(log);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ActivityLog>> GetForDateAsync(
            DateOnly date,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ActivityLog>>(Logs.Where(log => log.Date == date).ToArray());

        public Task<IReadOnlyList<ActivityLog>> GetForRangeAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ActivityLog>>(
                Logs.Where(log => log.Date >= startDate && log.Date <= endDate).ToArray());
    }
}
