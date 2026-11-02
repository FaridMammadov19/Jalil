namespace Jalil.Core;

public static class ReactionEmojiMatcher
{
    public static bool IsMatch(string? telegramEmoji, string? configuredEmoji)
        => !string.IsNullOrWhiteSpace(telegramEmoji) &&
           !string.IsNullOrWhiteSpace(configuredEmoji) &&
           string.Equals(
               Normalize(telegramEmoji),
               Normalize(configuredEmoji),
               StringComparison.Ordinal);

    public static string Normalize(string emoji)
        => emoji.Trim().Replace("\uFE0F", string.Empty, StringComparison.Ordinal);
}
