using System.Text;

namespace Jalil.Core;

public static class UnicodeTextMatcher
{
    public static bool EqualsIgnoringCaseAndNormalization(string? left, string? right)
        => left is not null &&
           right is not null &&
           string.Equals(
               Normalize(left),
               Normalize(right),
               StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string value)
        => value.Trim().Normalize(NormalizationForm.FormKC);
}
