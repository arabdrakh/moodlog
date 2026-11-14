using MoodLog.Core.Functional;

namespace MoodLog.Tests;

public class ResultTests
{
    [Fact]
    public void Ok_ShouldCreateSuccessResult()
    {
        var result = Result<int, DomainError>.Ok(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Fail_ShouldCreateFailureResult()
    {
        var error = DomainError.Validation("Invalid value");
        var result = Result<int, DomainError>.Fail(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error.Code);
    }

    [Fact]
    public void Map_OnSuccess_ShouldTransformValue()
    {
        var result = Result<int, DomainError>.Ok(10)
            .Map(x => x * 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(20, result.Value);
    }

    [Fact]
    public void Map_OnFailure_ShouldKeepError()
    {
        var error = DomainError.Validation("Failed");
        var result = Result<int, DomainError>.Fail(error)
            .Map(x => x * 2);

        Assert.True(result.IsFailure);
        Assert.Equal("Failed", result.Error.Message);
    }

    [Fact]
    public void Bind_OnSuccess_ShouldChainResults()
    {
        Result<int, DomainError> DoubleIfPositive(int x) =>
            x > 0
                ? Result<int, DomainError>.Ok(x * 2)
                : Result<int, DomainError>.Fail(DomainError.Validation("Must be positive"));

        var result = Result<int, DomainError>.Ok(5).Bind(DoubleIfPositive);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value);
    }

    [Fact]
    public void Ensure_PredicateFails_ShouldReturnFailure()
    {
        var result = Result<int, DomainError>.Ok(-5)
            .Ensure(x => x >= 0, DomainError.Validation("Negative not allowed"));

        Assert.True(result.IsFailure);
        Assert.Equal("Negative not allowed", result.Error.Message);
    }

    [Fact]
    public void LinqSelectMany_ShouldWorkMonadically()
    {
        var query =
            from a in Result<int, DomainError>.Ok(10)
            from b in Result<int, DomainError>.Ok(20)
            select a + b;

        Assert.True(query.IsSuccess);
        Assert.Equal(30, query.Value);
    }
}
