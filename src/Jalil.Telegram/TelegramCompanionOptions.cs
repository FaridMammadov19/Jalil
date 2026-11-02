namespace Jalil.Telegram;

public sealed record TelegramCompanionOptions(
    string FolderName,
    string AddCommand,
    string RemoveCommand,
    bool DeleteCommandMessages,
    string SessionPath,
    string UpdateStatePath,
    bool DryRun = false,
    bool ListFolders = false);
