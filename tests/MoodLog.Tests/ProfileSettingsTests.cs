using MoodLog.Core.Interfaces;
using MoodLog.Core.Models;
using MoodLog.Core.Services;

namespace MoodLog.Tests;

public class ProfileSettingsTests
{
    [Fact]
    public void Create_ShouldTrimNameAndApplySettings()
    {
        var result = ProfileSettings.Create("  Alex  ", 2500, 450, 10000);

        Assert.True(result.IsSuccess);
        Assert.Equal("Alex", result.Value.DisplayName);
        Assert.Equal(2500, result.Value.DailyWaterGoalMl);
        Assert.Equal(450, result.Value.DailySleepGoalMinutes);
        Assert.Equal(10000, result.Value.DailyStepGoal);
    }

    [Theory]
    [InlineData("", 2000, 480, 8000)]
    [InlineData("Name", 499, 480, 8000)]
    [InlineData("Name", 2000, 239, 8000)]
    [InlineData("Name", 2000, 480, 999)]
    public void Create_ShouldRejectInvalidSettings(
        string displayName,
        int waterGoalMl,
        int sleepGoalMinutes,
        int stepGoal)
    {
        var result = ProfileSettings.Create(displayName, waterGoalMl, sleepGoalMinutes, stepGoal);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GetAsync_ShouldUseDefaultSettingsWhenNoProfileExists()
    {
        var service = new ProfileSettingsService(new TestProfileRepository());
        var settings = await service.GetAsync();
        Assert.Equal(ProfileSettings.Default, settings);
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistValidSettingsAndKeepInvalidSettingsOut()
    {
        var repository = new TestProfileRepository();
        var service = new ProfileSettingsService(repository);

        var result = await service.SaveAsync("Taylor", 2400, 480, 9000);
        var invalid = await service.SaveAsync(" ", 2400, 480, 9000);

        Assert.True(result.IsSuccess);
        Assert.True(invalid.IsFailure);
        Assert.Equal("Taylor", (await service.GetAsync()).DisplayName);
    }

    private sealed class TestProfileRepository : IProfileSettingsRepository
    {
        private ProfileSettings? _settings;

        public Task<ProfileSettings?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_settings);

        public Task SaveAsync(ProfileSettings settings, CancellationToken cancellationToken = default)
        {
            _settings = settings;
            return Task.CompletedTask;
        }
    }
}
