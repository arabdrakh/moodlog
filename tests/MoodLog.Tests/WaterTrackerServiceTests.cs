using MoodLog.Core.Interfaces;
using MoodLog.Core.Models;
using MoodLog.Core.Services;

namespace MoodLog.Tests;

public class WaterTrackerServiceTests
{
    [Fact]
    public async Task AddAsync_ShouldPersistValidWaterLog()
    {
        var repository = new TestWaterLogRepository();
        var service = new WaterTrackerService(repository);

        var result = await service.AddAsync(new DateOnly(2026, 11, 13), 250);

        Assert.True(result.IsSuccess);
        Assert.Equal(result.Value.Id, Assert.Single(repository.Logs).Id);
    }

    [Fact]
    public async Task AddAsync_ShouldNotPersistInvalidWaterLog()
    {
        var repository = new TestWaterLogRepository();
        var service = new WaterTrackerService(repository);

        var result = await service.AddAsync(new DateOnly(2026, 11, 13), 0);

        Assert.True(result.IsFailure);
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task GetDailyProgressAsync_ShouldReturnDefaultGoalForEmptyDay()
    {
        var service = new WaterTrackerService(new TestWaterLogRepository());

        var progress = await service.GetDailyProgressAsync(new DateOnly(2026, 11, 13));

        Assert.Equal(0, progress.ConsumedMl);
        Assert.Equal(2000, progress.DailyGoalMl);
        Assert.Equal(2000, progress.RemainingMl);
        Assert.Equal(0, progress.ProgressPercent);
        Assert.False(progress.GoalReached);
    }

    [Fact]
    public async Task GetDailyProgressAsync_ShouldSumLogsAndCapProgressAtGoal()
    {
        var repository = new TestWaterLogRepository();
        var service = new WaterTrackerService(repository);
        var date = new DateOnly(2026, 11, 13);
        repository.Logs.Add(WaterLog.Create(date, 1200, 2000).Value);
        repository.Logs.Add(WaterLog.Create(date, 1000, 2000).Value);

        var progress = await service.GetDailyProgressAsync(date);

        Assert.Equal(2200, progress.ConsumedMl);
        Assert.Equal(0, progress.RemainingMl);
        Assert.Equal(100, progress.ProgressPercent);
        Assert.True(progress.GoalReached);
    }

    [Fact]
    public async Task GetDailyProgressAsync_ShouldUseCurrentProfileGoalNotOldLogGoal()
    {
        var repository = new TestWaterLogRepository();
        var date = new DateOnly(2026, 11, 13);
        repository.Logs.Add(WaterLog.Create(date, 250, 2000).Value);
        var progress = await new WaterTrackerService(repository).GetDailyProgressAsync(date, 3200);
        Assert.Equal(3200, progress.DailyGoalMl);
        Assert.Equal(2950, progress.RemainingMl);
    }

    private sealed class TestWaterLogRepository : IWaterLogRepository
    {
        public Task<WaterLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Logs.FirstOrDefault(item => item.Id == id));

        public Task<bool> UpdateAsync(WaterLog item, CancellationToken cancellationToken = default)
        {
            var index = Logs.FindIndex(old => old.Id == item.Id);
            if (index == -1) return Task.FromResult(false);
            Logs[index] = item;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Logs.RemoveAll(item => item.Id == id) > 0);

        public List<WaterLog> Logs { get; } = [];

        public Task AddAsync(WaterLog log, CancellationToken cancellationToken = default)
        {
            Logs.Add(log);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WaterLog>> GetForDateAsync(
            DateOnly date,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WaterLog>>(
                Logs.Where(log => log.Date == date).ToArray());

        public Task<IReadOnlyList<WaterLog>> GetForRangeAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WaterLog>>(
                Logs.Where(log => log.Date >= startDate && log.Date <= endDate).ToArray());
    }
}
