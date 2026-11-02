namespace Jalil.Core;

public static class CommandMatcher
{
    public static bool IsMatch(
        string? messageText,
        string? configuredCommand,
        bool isOutgoing,
        bool hasMedia)
    {
        if (!isOutgoing ||
            hasMedia ||
            string.IsNullOrWhiteSpace(messageText) ||
            string.IsNullOrWhiteSpace(configuredCommand))
        {
            return false;
        }

        return string.Equals(
            messageText.Trim(),
            configuredCommand.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }
}
