namespace Jalil.Core;

public static class DoneCommand
{
    public const string Text = "/done";

    public static bool IsMatch(string? text, bool isOutgoing, bool hasMedia)
        => CommandMatcher.IsMatch(text, Text, isOutgoing, hasMedia);
}
