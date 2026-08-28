using FluentAssertions;
using MyFrete.BuildingBlocks.Results;
using Xunit;

namespace MyFrete.Tests.Unit;

public class ResultTests
{
    [Fact]
    public void Success_carries_value_and_no_error()
    {
        Result<int> result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_exposes_error_and_hides_value()
    {
        var error = Error.Conflict("x.taken", "already taken");
        Result<int> result = error;

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
        var act = () => _ = result.Value;
        act.Should().Throw<InvalidOperationException>();
    }

}
