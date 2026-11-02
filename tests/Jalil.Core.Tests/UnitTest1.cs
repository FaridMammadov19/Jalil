using Jalil.Core;

namespace Jalil.Core.Tests;

public sealed class DoneCommandTests
{
    [Theory]
    [InlineData("/done")]
    [InlineData("/DONE")]
    [InlineData("  /DoNe\r\n")]
    public void MatchesExactOutgoingText(string text)
    {
        Assert.True(DoneCommand.IsMatch(text, isOutgoing: true, hasMedia: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/done now")]
    [InlineData("please /done")]
    [InlineData("/done!")]
    public void RejectsNonExactText(string? text)
    {
        Assert.False(DoneCommand.IsMatch(text, isOutgoing: true, hasMedia: false));
    }

    [Fact]
    public void RejectsIncomingCommand()
    {
        Assert.False(DoneCommand.IsMatch("/done", isOutgoing: false, hasMedia: false));
    }

    [Fact]
    public void RejectsMediaCaption()
    {
        Assert.False(DoneCommand.IsMatch("/done", isOutgoing: true, hasMedia: true));
    }
}

public sealed class UndoCommandTests
{
    [Theory]
    [InlineData("/undo")]
    [InlineData("/UNDO")]
    [InlineData("  /UnDo\r\n")]
    public void MatchesExactOutgoingText(string text)
    {
        Assert.True(UndoCommand.IsMatch(text, isOutgoing: true, hasMedia: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/undo now")]
    [InlineData("please /undo")]
    [InlineData("/undo!")]
    public void RejectsNonExactText(string? text)
    {
        Assert.False(UndoCommand.IsMatch(text, isOutgoing: true, hasMedia: false));
    }

    [Fact]
    public void RejectsIncomingCommand()
    {
        Assert.False(UndoCommand.IsMatch("/undo", isOutgoing: false, hasMedia: false));
    }

    [Fact]
    public void RejectsMediaCaption()
    {
        Assert.False(UndoCommand.IsMatch("/undo", isOutgoing: true, hasMedia: true));
    }
}

public sealed class CommandMatcherTests
{
    [Fact]
    public void MatchesAConfiguredCommand()
    {
        Assert.True(CommandMatcher.IsMatch(
            "  /finished  ",
            "/finished",
            isOutgoing: true,
            hasMedia: false));
    }

    [Fact]
    public void DoesNotMatchAFormerDefaultCommand()
    {
        Assert.False(CommandMatcher.IsMatch(
            "/done",
            "/finished",
            isOutgoing: true,
            hasMedia: false));
    }

    [Fact]
    public void RejectsAnEmptyConfiguredCommand()
    {
        Assert.False(CommandMatcher.IsMatch(
            "/done",
            " ",
            isOutgoing: true,
            hasMedia: false));
    }
}
