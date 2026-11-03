using System.Collections.Concurrent;
using Jalil.Core;
using TL;

namespace Jalil.Telegram;

public sealed class TelegramCompanion(TelegramCompanionOptions options, Func<string, string?> loginConfiguration)
{
    private const int MainChatListFolderId = 0;
    private const int ArchiveFolderId = 1;
    private const int DialogPollingLimit = 100;
    private readonly ConcurrentQueue<Update> _pendingUpdates = new();
    private readonly HashSet<string> _completedEvents = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _actionLock = new(1, 1);
    private WTelegram.Client? _client;
    private WTelegram.UpdateManager? _manager;
    private FolderMutationService? _folderService;
    private ArchiveMutationService? _archiveService;
    private ReactionStateStore? _reactionStateStore;
    private volatile bool _ready;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        WTelegram.Helpers.Log = (_, _) => { };

        await using var client = new WTelegram.Client(loginConfiguration);
        _client = client;
        _folderService = new FolderMutationService(client);
        _archiveService = new ArchiveMutationService(client);
        _reactionStateStore = new ReactionStateStore(options.ReactionStatePath);
        _manager = client.WithUpdateManager(HandleUpdateAsync, options.UpdateStatePath);

        try
        {
            Console.WriteLine("Authorizing Telegram account...");
            await client.LoginUserIfNeeded();
            Console.WriteLine("Telegram authorization succeeded.");

            var dialogs = await client.Messages_GetAllDialogs();
            dialogs.CollectUsersChats(_manager.Users, _manager.Chats);

            if (options.ListFolders)
            {
                var names = await _folderService.ListFolderNamesAsync();
                Console.WriteLine(names.Count == 0
                    ? "No custom Telegram folders were found."
                    : $"Telegram folders:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", names)}");
                return;
            }

            if (options.ReactionRoutingEnabled)
            {
                await PollLatestDialogReactionsAsync(applyActions: false, cancellationToken);
            }

            _ready = true;
            while (_pendingUpdates.TryDequeue(out var update))
            {
                await ProcessUpdateAsync(update, cancellationToken);
            }

            var targetDescription = options.Destination == RoutingDestination.Archive
                ? "Telegram Archive"
                : $"folder '{options.FolderName}'";
            Console.WriteLine(options.DryRun
                ? $"Jalil is watching for {options.AddCommand} and {options.RemoveCommand} (dry run: nothing will change)."
                : $"Jalil is watching for {options.AddCommand} and {options.RemoveCommand}. Target: {targetDescription}");
            if (options.ReactionRoutingEnabled)
            {
                Console.WriteLine(
                    $"Reaction routing is enabled: {options.AddReactionEmoji} adds, {options.RemoveReactionEmoji} removes " +
                    $"(latest-message polling every {options.ReactionPollInterval.TotalSeconds:0} seconds).");
            }
            Console.WriteLine("Press Ctrl+C to stop.");

            if (options.ReactionRoutingEnabled)
            {
                await RunReactionPollingLoopAsync(cancellationToken);
            }
            else
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        finally
        {
            _ready = false;
            _manager.SaveState(options.UpdateStatePath);
        }
    }

    private Task HandleUpdateAsync(Update update)
    {
        if (!_ready)
        {
            _pendingUpdates.Enqueue(update);
            return Task.CompletedTask;
        }

        return ProcessUpdateSafelyAsync(update);
    }

    private async Task ProcessUpdateSafelyAsync(Update update)
    {
        try
        {
            await ProcessUpdateAsync(update, CancellationToken.None);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Could not process a Telegram update ({exception.GetType().Name}): {exception.Message}");
        }
    }

    private async Task ProcessUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        switch (update)
        {
            case UpdateNewMessage { message: Message message }:
                await ProcessCommandMessageAsync(message, cancellationToken);
                break;
            case UpdateMessageReactions reactionUpdate when options.ReactionRoutingEnabled:
                await ProcessReactionUpdateAsync(reactionUpdate, cancellationToken);
                break;
        }
    }

    private async Task ProcessCommandMessageAsync(Message message, CancellationToken cancellationToken)
    {
        var isOutgoing = message.flags.HasFlag(Message.Flags.out_);
        var hasMedia = message.flags.HasFlag(Message.Flags.has_media);
        var isAdd = CommandMatcher.IsMatch(
            message.message,
            options.AddCommand,
            isOutgoing,
            hasMedia);
        var isRemove = CommandMatcher.IsMatch(
            message.message,
            options.RemoveCommand,
            isOutgoing,
            hasMedia);
        if (!isAdd && !isRemove)
        {
            return;
        }

        if (_manager?.UserOrChat(message.peer_id) is not { } peerInfo || _folderService is null)
        {
            Console.Error.WriteLine("Recognized a Jalil command, but could not resolve its conversation.");
            return;
        }

        var peer = peerInfo.ToInputPeer();
        var commandKey = $"{peer.GetType().Name}:{peer.ID}:{message.id}";

        await _actionLock.WaitAsync(cancellationToken);
        try
        {
            if (_completedEvents.Contains(commandKey))
            {
                return;
            }

            var result = await ApplyFolderActionAsync(peer, isAdd, cancellationToken);

            if (options.DeleteCommandMessages && !options.DryRun)
            {
                if (_client is null)
                {
                    throw new InvalidOperationException("Telegram client is not available to delete the command message.");
                }

                await _client.DeleteMessages(peer, message.id);
            }

            _completedEvents.Add(commandKey);
            WriteResult(result, isAdd ? options.AddCommand : options.RemoveCommand);
        }
        finally
        {
            _actionLock.Release();
        }
    }

    private async Task ProcessReactionUpdateAsync(
        UpdateMessageReactions update,
        CancellationToken cancellationToken)
    {
        if (_manager?.UserOrChat(update.peer) is not { } peerInfo ||
            _folderService is null ||
            _reactionStateStore is null)
        {
            Console.Error.WriteLine("Recognized a reaction update, but could not resolve its conversation.");
            return;
        }

        var peer = peerInfo.ToInputPeer();
        var reactions = await GetAuthoritativeReactionStateAsync(update, peer);
        await ProcessReactionStateAsync(
            peer,
            update.msg_id,
            reactions,
            applyActions: true,
            cancellationToken);
    }

    private async Task RunReactionPollingLoopAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(options.ReactionPollInterval, cancellationToken);

            try
            {
                await PollLatestDialogReactionsAsync(applyActions: true, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    $"Could not poll Telegram reactions ({exception.GetType().Name}): {exception.Message}");
            }
        }
    }

    private async Task PollLatestDialogReactionsAsync(
        bool applyActions,
        CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            throw new InvalidOperationException("Telegram client is not available to poll reactions.");
        }

        foreach (var folderId in new[] { MainChatListFolderId, ArchiveFolderId })
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dialogs = await _client.Messages_GetDialogs(
                default,
                0,
                InputPeer.Self,
                DialogPollingLimit,
                0,
                folderId,
                false);

            foreach (var message in dialogs.Messages.OfType<Message>())
            {
                if (dialogs.UserOrChat(message.peer_id) is not { } peerInfo)
                {
                    continue;
                }

                await ProcessReactionStateAsync(
                    peerInfo.ToInputPeer(),
                    message.id,
                    message.reactions,
                    applyActions,
                    cancellationToken);
            }
        }
    }

    private async Task ProcessReactionStateAsync(
        InputPeer peer,
        int messageId,
        MessageReactions? reactions,
        bool applyActions,
        CancellationToken cancellationToken)
    {
        var stateStore = _reactionStateStore
                         ?? throw new InvalidOperationException("Reaction state store is not available.");
        var addState = GetOwnReactionState(reactions, options.AddReactionEmoji);
        var removeState = GetOwnReactionState(reactions, options.RemoveReactionEmoji);
        var addKey = ReactionKey(peer, messageId, options.AddReactionEmoji);
        var removeKey = ReactionKey(peer, messageId, options.RemoveReactionEmoji);

        await _actionLock.WaitAsync(cancellationToken);
        try
        {
            var addWasKnown = stateStore.TryGetSelected(addKey, out var addWasSelected);
            var removeWasKnown = stateStore.TryGetSelected(removeKey, out var removeWasSelected);
            var addIsNew = ReactionEdgeDetector.IsNewSelection(
                addWasKnown,
                addWasSelected,
                addState.IsSelected);
            var removeIsNew = ReactionEdgeDetector.IsNewSelection(
                removeWasKnown,
                removeWasSelected,
                removeState.IsSelected);

            bool? shouldAdd = (addIsNew, removeIsNew) switch
            {
                (true, false) => true,
                (false, true) => false,
                (true, true) => addState.ChosenOrder >= removeState.ChosenOrder,
                _ => null
            };

            if (applyActions && shouldAdd is not null)
            {
                var result = await ApplyFolderActionAsync(peer, shouldAdd.Value, cancellationToken);
                WriteResult(
                    result,
                    shouldAdd.Value ? options.AddReactionEmoji : options.RemoveReactionEmoji);
            }

            var stateChanged = stateStore.SetSelected(addKey, addState.IsSelected);
            stateChanged |= stateStore.SetSelected(removeKey, removeState.IsSelected);
            if (stateChanged)
            {
                stateStore.Save();
            }
        }
        finally
        {
            _actionLock.Release();
        }
    }

    private async Task<MessageReactions> GetAuthoritativeReactionStateAsync(
        UpdateMessageReactions update,
        InputPeer peer)
    {
        if (_client is null)
        {
            throw new InvalidOperationException("Telegram client is not available to fetch reaction details.");
        }

        var response = await _client.Messages_GetMessagesReactions(peer, update.msg_id);
        var fullUpdate = response switch
        {
            UpdateShort { update: UpdateMessageReactions reactionUpdate } => reactionUpdate,
            Updates updates => updates.updates.OfType<UpdateMessageReactions>().FirstOrDefault(),
            UpdatesCombined updates => updates.updates.OfType<UpdateMessageReactions>().FirstOrDefault(),
            _ => null
        };

        return fullUpdate?.reactions
               ?? throw new InvalidOperationException("Telegram did not return the requested authoritative reaction state.");
    }

    private async Task<FolderMutationStatus> ApplyFolderActionAsync(
        InputPeer peer,
        bool shouldAdd,
        CancellationToken cancellationToken)
    {
        if (options.Destination == RoutingDestination.Archive)
        {
            if (_archiveService is null)
            {
                throw new InvalidOperationException("Archive service is not available.");
            }

            return await _archiveService.SetArchivedAsync(
                peer,
                shouldAdd,
                options.DryRun,
                cancellationToken);
        }

        if (_folderService is null)
        {
            throw new InvalidOperationException("Folder service is not available.");
        }

        return shouldAdd
            ? await _folderService.AddToFolderAsync(
                peer,
                options.FolderName,
                options.DryRun,
                cancellationToken)
            : await _folderService.RemoveFromFolderAsync(
                peer,
                options.FolderName,
                options.DryRun,
                cancellationToken);
    }

    private static OwnReactionState GetOwnReactionState(MessageReactions? reactions, string configuredEmoji)
    {
        var selected = (reactions?.results ?? [])
            .Where(item => item.flags.HasFlag(ReactionCount.Flags.has_chosen_order))
            .Where(item => item.reaction is ReactionEmoji emoji &&
                           ReactionEmojiMatcher.IsMatch(emoji.emoticon, configuredEmoji))
            .OrderByDescending(item => item.chosen_order)
            .FirstOrDefault();
        return new OwnReactionState(
            selected is not null,
            selected?.chosen_order ?? -1);
    }

    private static string ReactionKey(InputPeer peer, int messageId, string emoji)
        => $"{peer.GetType().Name}:{peer.ID}:{messageId}:{ReactionEmojiMatcher.Normalize(emoji)}";

    private static void WriteResult(FolderMutationStatus result, string trigger)
    {
        Console.WriteLine(result switch
        {
            FolderMutationStatus.Added => $"{trigger}: moved conversation to the configured folder.",
            FolderMutationStatus.AlreadyPresent => $"{trigger}: conversation is already in the configured folder.",
            FolderMutationStatus.WouldAdd => $"{trigger}: dry run would move the conversation.",
            FolderMutationStatus.Removed => $"{trigger}: removed conversation from the configured folder.",
            FolderMutationStatus.AlreadyAbsent => $"{trigger}: conversation is already absent from the configured folder.",
            FolderMutationStatus.WouldRemove => $"{trigger}: dry run would remove the conversation.",
            FolderMutationStatus.Archived => $"{trigger}: archived conversation.",
            FolderMutationStatus.Restored => $"{trigger}: restored conversation from Archive.",
            FolderMutationStatus.WouldArchive => $"{trigger}: dry run would archive the conversation.",
            FolderMutationStatus.WouldRestore => $"{trigger}: dry run would restore the conversation from Archive.",
            _ => $"Processed {trigger}."
        });
    }

    private sealed record OwnReactionState(
        bool IsSelected,
        int ChosenOrder);
}
