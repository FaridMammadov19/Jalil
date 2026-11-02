using System.Collections.Concurrent;
using Jalil.Core;
using TL;

namespace Jalil.Telegram;

public sealed class TelegramCompanion(TelegramCompanionOptions options, Func<string, string?> loginConfiguration)
{
    private readonly ConcurrentQueue<Update> _pendingUpdates = new();
    private readonly HashSet<string> _completedCommands = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private WTelegram.Client? _client;
    private WTelegram.UpdateManager? _manager;
    private FolderMutationService? _folderService;
    private volatile bool _ready;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        WTelegram.Helpers.Log = (_, _) => { };

        await using var client = new WTelegram.Client(loginConfiguration);
        _client = client;
        _folderService = new FolderMutationService(client);
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

            _ready = true;
            while (_pendingUpdates.TryDequeue(out var update))
            {
                await ProcessUpdateAsync(update, cancellationToken);
            }

            Console.WriteLine(options.DryRun
                ? $"Jalil is watching for {options.AddCommand} and {options.RemoveCommand} (dry run: nothing will change)."
                : $"Jalil is watching for {options.AddCommand} and {options.RemoveCommand}. Target folder: {options.FolderName}");
            Console.WriteLine("Press Ctrl+C to stop.");

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
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
        if (update is not UpdateNewMessage { message: Message message })
        {
            return;
        }

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

        await _commandLock.WaitAsync(cancellationToken);
        try
        {
            if (_completedCommands.Contains(commandKey))
            {
                return;
            }

            var result = isAdd
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

            if (options.DeleteCommandMessages && !options.DryRun)
            {
                if (_client is null)
                {
                    throw new InvalidOperationException("Telegram client is not available to delete the command message.");
                }

                await _client.DeleteMessages(peer, message.id);
            }

            _completedCommands.Add(commandKey);
            Console.WriteLine(result switch
            {
                FolderMutationStatus.Added => $"Moved conversation to '{options.FolderName}'.",
                FolderMutationStatus.AlreadyPresent => $"Conversation is already in '{options.FolderName}'.",
                FolderMutationStatus.WouldAdd => $"Dry run: would move conversation to '{options.FolderName}'.",
                FolderMutationStatus.Removed => $"Removed conversation from '{options.FolderName}'.",
                FolderMutationStatus.AlreadyAbsent => $"Conversation is already absent from '{options.FolderName}'.",
                FolderMutationStatus.WouldRemove => $"Dry run: would remove conversation from '{options.FolderName}'.",
                _ => "Processed Jalil command."
            });
        }
        finally
        {
            _commandLock.Release();
        }
    }
}
