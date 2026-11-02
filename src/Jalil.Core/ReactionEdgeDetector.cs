namespace Jalil.Core;

public static class ReactionEdgeDetector
{
    public static bool IsNewSelection(
        bool previousStateWasKnown,
        bool wasSelected,
        bool isSelected,
        bool isFreshOwnReaction)
        => isSelected &&
           !wasSelected &&
           (previousStateWasKnown || isFreshOwnReaction);
}
