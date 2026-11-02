using TL;

namespace Jalil.Telegram;

internal enum FolderMutationStatus
{
    Added,
    AlreadyPresent,
    WouldAdd,
    Removed,
    AlreadyAbsent,
    WouldRemove
}
internal sealed class FolderMutationService(WTelegram.Client client)
{
    public async Task<IReadOnlyList<string>> ListFolderNamesAsync()
    {
        var response = await client.Messages_GetDialogFilters();

        // Telegram represents the built-in "All Chats" entry as a null
        // DialogFilterBase (dialogFilterDefault). It is not a custom folder.
        return (response.filters ?? [])
            .OfType<DialogFilterBase>()
            .Select(filter => filter.Title?.text)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<FolderMutationStatus> AddToFolderAsync(
        InputPeer peer,
        string folderName,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = await client.Messages_GetDialogFilters();
        var matching = (response.filters ?? [])
            .OfType<DialogFilterBase>()
            .Where(filter => string.Equals(
                filter.Title?.text?.Trim(),
                folderName.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matching.Length == 0)
        {
            throw new InvalidOperationException(
                $"No Telegram folder named '{folderName}' exists. Run with --list-folders to see available folders.");
        }

        if (matching.Length > 1)
        {
            throw new InvalidOperationException(
                $"More than one Telegram folder is named '{folderName}'. Rename one of them before continuing.");
        }

        if (matching[0] is not DialogFilter target)
        {
            throw new InvalidOperationException(
                $"The folder '{folderName}' is a shared/imported folder. Jalil currently supports regular Telegram folders only.");
        }

        var included = target.include_peers ?? [];
        var excluded = target.exclude_peers ?? [];
        var alreadyIncluded = included.Any(item => SamePeer(item, peer));
        var isExcluded = excluded.Any(item => SamePeer(item, peer));

        if (alreadyIncluded && !isExcluded)
        {
            return FolderMutationStatus.AlreadyPresent;
        }

        if (dryRun)
        {
            return FolderMutationStatus.WouldAdd;
        }

        if (!alreadyIncluded)
        {
            target.include_peers = [.. included, peer];
        }

        if (isExcluded)
        {
            target.exclude_peers = excluded.Where(item => !SamePeer(item, peer)).ToArray();
        }

        cancellationToken.ThrowIfCancellationRequested();
        var accepted = await client.Messages_UpdateDialogFilter(target.id, target);
        if (!accepted)
        {
            throw new InvalidOperationException("Telegram did not accept the folder update.");
        }

        var verification = await client.Messages_GetDialogFilters();
        var verifiedTarget = (verification.filters ?? [])
            .OfType<DialogFilter>()
            .SingleOrDefault(filter => filter.id == target.id);

        if (verifiedTarget is null ||
            !(verifiedTarget.include_peers ?? []).Any(item => SamePeer(item, peer)) ||
            (verifiedTarget.exclude_peers ?? []).Any(item => SamePeer(item, peer)))
        {
            throw new InvalidOperationException("Telegram returned an unexpected folder state after the update.");
        }

        return FolderMutationStatus.Added;
    }

    public async Task<FolderMutationStatus> RemoveFromFolderAsync(
        InputPeer peer,
        string folderName,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = await client.Messages_GetDialogFilters();
        var matching = (response.filters ?? [])
            .OfType<DialogFilterBase>()
            .Where(filter => string.Equals(
                filter.Title?.text?.Trim(),
                folderName.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matching.Length == 0)
        {
            throw new InvalidOperationException(
                $"No Telegram folder named '{folderName}' exists. Run with --list-folders to see available folders.");
        }

        if (matching.Length > 1)
        {
            throw new InvalidOperationException(
                $"More than one Telegram folder is named '{folderName}'. Rename one of them before continuing.");
        }

        if (matching[0] is not DialogFilter target)
        {
            throw new InvalidOperationException(
                $"The folder '{folderName}' is a shared/imported folder. Jalil currently supports regular Telegram folders only.");
        }

        var included = target.include_peers ?? [];
        var excluded = target.exclude_peers ?? [];
        var pinned = target.pinned_peers ?? [];
        var isIncluded = included.Any(item => SamePeer(item, peer));
        var isExcluded = excluded.Any(item => SamePeer(item, peer));
        var isPinned = pinned.Any(item => SamePeer(item, peer));

        if (!isIncluded && !isPinned && isExcluded)
        {
            return FolderMutationStatus.AlreadyAbsent;
        }

        if (dryRun)
        {
            return FolderMutationStatus.WouldRemove;
        }

        target.include_peers = included.Where(item => !SamePeer(item, peer)).ToArray();
        target.pinned_peers = pinned.Where(item => !SamePeer(item, peer)).ToArray();
        if (!isExcluded)
        {
            target.exclude_peers = [.. excluded, peer];
        }

        cancellationToken.ThrowIfCancellationRequested();
        var accepted = await client.Messages_UpdateDialogFilter(target.id, target);
        if (!accepted)
        {
            throw new InvalidOperationException("Telegram did not accept the folder update.");
        }

        var verification = await client.Messages_GetDialogFilters();
        var verifiedTarget = (verification.filters ?? [])
            .OfType<DialogFilter>()
            .SingleOrDefault(filter => filter.id == target.id);

        if (verifiedTarget is null ||
            (verifiedTarget.include_peers ?? []).Any(item => SamePeer(item, peer)) ||
            (verifiedTarget.pinned_peers ?? []).Any(item => SamePeer(item, peer)) ||
            !(verifiedTarget.exclude_peers ?? []).Any(item => SamePeer(item, peer)))
        {
            throw new InvalidOperationException("Telegram returned an unexpected folder state after the update.");
        }

        return FolderMutationStatus.Removed;
    }

    private static bool SamePeer(InputPeer left, InputPeer right) => (left, right) switch
    {
        (InputPeerSelf, InputPeerSelf) => true,
        (InputPeerChat a, InputPeerChat b) => a.chat_id == b.chat_id,
        (InputPeerUser a, InputPeerUser b) => a.user_id == b.user_id,
        (InputPeerChannel a, InputPeerChannel b) => a.channel_id == b.channel_id,
        (InputPeerUserFromMessage a, InputPeerUserFromMessage b) => a.user_id == b.user_id,
        (InputPeerChannelFromMessage a, InputPeerChannelFromMessage b) => a.channel_id == b.channel_id,
        _ => false
    };
}
