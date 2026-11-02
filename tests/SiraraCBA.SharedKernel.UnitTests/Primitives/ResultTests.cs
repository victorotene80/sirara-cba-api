namespace SiraraCBA.SharedKernel.UnitTests.Primitives;

public sealed class ResultTests
{
    [Fact]
    public void Success_CarryingAnError_IsAProgrammingError()
    {
        Should.Throw<InvalidOperationException>(() => Result.Failure(Error.None));
    }

    [Fact]
    public void Value_OfAFailedResult_CannotBeRead()
    {
        Result<int> result = Result.Failure<int>(Error.NotFound("x.missing", "gone"));

        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Pending_IsAFirstClassOutcome_NotAFailureToBeLoggedAsAnError()
    {
        Guid approvalRequestId = Guid.CreateVersion7();

        Result result = Result.Pending(approvalRequestId);

        result.IsPending.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Pending);
        result.Error.Message.ShouldBe(approvalRequestId.ToString());
    }

    [Fact]
    public void Success_WithAValue_ExposesIt()
    {
        Result<string> result = Result.Success("posted");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("posted");
    }
}
