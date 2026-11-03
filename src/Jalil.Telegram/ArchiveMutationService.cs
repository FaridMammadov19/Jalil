using TL;

namespace Jalil.Telegram;

internal sealed class ArchiveMutationService(WTelegram.Client client)
{
    private const int MainChatListFolderId = 0;
    private const int ArchiveFolderId = 1;

    public async Task<FolderMutationStatus> SetArchivedAsync(
        InputPeer peer,
        bool shouldArchive,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (dryRun)
        {
            return shouldArchive
                ? FolderMutationStatus.WouldArchive
                : FolderMutationStatus.WouldRestore;
        }

        var targetFolderId = shouldArchive ? ArchiveFolderId : MainChatListFolderId;
        var response = await client.Folders_EditPeerFolders(new InputFolderPeer
        {
            peer = peer,
            folder_id = targetFolderId
        });

        var update = response switch
        {
            UpdateShort { update: UpdateFolderPeers folderUpdate } => folderUpdate,
            Updates updates => updates.updates.OfType<UpdateFolderPeers>().FirstOrDefault(),
            UpdatesCombined updates => updates.updates.OfType<UpdateFolderPeers>().FirstOrDefault(),
            _ => null
        };

        if (update is null ||
            !(update.folder_peers ?? []).Any(folderPeer =>
                folderPeer.folder_id == targetFolderId && folderPeer.peer.ID == peer.ID))
        {
            throw new InvalidOperationException("Telegram returned an unexpected archive state after the update.");
        }

        return shouldArchive
            ? FolderMutationStatus.Archived
            : FolderMutationStatus.Restored;
    }
}
