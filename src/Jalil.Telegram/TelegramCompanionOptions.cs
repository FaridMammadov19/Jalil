namespace Jalil.Telegram;

public sealed record TelegramCompanionOptions(
    string FolderName,
    string AddCommand,
    string RemoveCommand,
    bool DeleteCommandMessages,
    bool ReactionRoutingEnabled,
    string AddReactionEmoji,
    string RemoveReactionEmoji,
    string SessionPath,
    string UpdateStatePath,
    string ReactionStatePath,
    bool DryRun = false,
    bool ListFolders = false);
