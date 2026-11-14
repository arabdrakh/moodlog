using MoodLog.Core.Models;

namespace MoodLog.Tests;

public class WaterLogTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(5001)]
    public void Create_ShouldRejectAmountOutsideAllowedRange(int amountMl)
    {
        var result = WaterLog.Create(new DateOnly(2026, 11, 13), amountMl);

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error.Code);
    }

    [Theory]
    [InlineData(499)]
    [InlineData(10001)]
    public void Create_ShouldRejectDailyGoalOutsideAllowedRange(int dailyGoalMl)
    {
        var result = WaterLog.Create(new DateOnly(2026, 11, 13), 250, dailyGoalMl);

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error.Code);
    }

    [Fact]
    public void Create_ShouldAcceptValidLog()
    {
        var result = WaterLog.Create(new DateOnly(2026, 11, 13), 250, 2500);

        Assert.True(result.IsSuccess);
        Assert.Equal(250, result.Value.AmountMl);
        Assert.Equal(2500, result.Value.DailyGoalMl);
    }
}
