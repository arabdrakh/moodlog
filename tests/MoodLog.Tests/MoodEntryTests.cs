using MoodLog.Core.Models;

namespace MoodLog.Tests;

public class MoodEntryTests
{
    [Fact]
    public void Create_ShouldNormalizeTagsAndNote()
    {
        var result = MoodEntry.Create(
            new DateOnly(2026, 11, 13),
            MoodLevel.Good,
            [" work ", "WORK", "rest", " "],
            "  A good day  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("A good day", result.Value.Note);
        Assert.Equal(["work", "rest"], result.Value.Tags);
    }

    [Fact]
    public void Create_ShouldRejectNoteLongerThan500Characters()
    {
        var result = MoodEntry.Create(
            new DateOnly(2026, 11, 13),
            MoodLevel.Neutral,
            null,
            new string('a', 501));

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error.Code);
    }

    [Fact]
    public void Create_ShouldRejectUndefinedMoodLevel()
    {
        var result = MoodEntry.Create(
            new DateOnly(2026, 11, 13),
            (MoodLevel)0,
            null,
            null);

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error.Code);
    }

    [Fact]
    public void Create_ShouldRejectOverlyLongTag()
    {
        var result = MoodEntry.Create(
            new DateOnly(2026, 11, 13),
            MoodLevel.Good,
            [new string('a', 41)],
            null);

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error.Code);
    }

    [Fact]
    public void Create_ShouldLimitTagsToTen()
    {
        var tags = Enumerable.Range(1, 12).Select(number => $"tag-{number}");

        var result = MoodEntry.Create(
            new DateOnly(2026, 11, 13),
            MoodLevel.Neutral,
            tags,
            null);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value.Tags.Count);
    }
}
