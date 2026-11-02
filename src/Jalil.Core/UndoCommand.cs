namespace Jalil.Core;

public static class UndoCommand
{
    public const string Text = "/undo";

    public static bool IsMatch(string? text, bool isOutgoing, bool hasMedia)
        => CommandMatcher.IsMatch(text, Text, isOutgoing, hasMedia);
}
