namespace Jalil.Telegram;

public enum RoutingDestination
{
    Folder,
    Archive
}

public sealed record TelegramCompanionOptions(
    RoutingDestination Destination,
    string FolderName,
    string AddCommand,
    string RemoveCommand,
    bool DeleteCommandMessages,
    bool ReactionRoutingEnabled,
    TimeSpan ReactionPollInterval,
    string AddReactionEmoji,
    string RemoveReactionEmoji,
    string SessionPath,
    string UpdateStatePath,
    string ReactionStatePath,
    bool DryRun = false,
    bool ListFolders = false);
