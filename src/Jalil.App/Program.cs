using System.Reflection;
using System.Runtime.InteropServices;
using Jalil.App;
using Jalil.Telegram;

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) ||
    args.Contains("-h", StringComparer.OrdinalIgnoreCase))
{
    PrintHelp();
    return 0;
}

var listFolders = args.Contains("--list-folders", StringComparer.OrdinalIgnoreCase);
var dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
var explicitSettingsPath = GetOption(args, "--settings")
    ?? Environment.GetEnvironmentVariable("JALIL_SETTINGS_PATH");
var settingsPath = ResolveSettingsPath(explicitSettingsPath);
JalilAppSettings settings;

try
{
    settings = JalilAppSettings.Load(settingsPath);
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Could not load settings ({exception.GetType().Name}): {exception.Message}");
    return 2;
}

var folderName = GetOption(args, "--folder")
    ?? Environment.GetEnvironmentVariable("JALIL_DONE_FOLDER")
    ?? settings.Routing.FolderName;
if (!Enum.TryParse<RoutingDestination>(
        settings.Routing.Destination,
        ignoreCase: true,
        out var routingDestination))
{
    Console.Error.WriteLine("Routing.Destination must be either 'Archive' or 'Folder'.");
    return 2;
}

var addCommand = settings.Routing.AddCommand?.Trim();
var removeCommand = settings.Routing.RemoveCommand?.Trim();
var addReactionEmoji = settings.ReactionRouting.AddEmoji?.Trim();
var removeReactionEmoji = settings.ReactionRouting.RemoveEmoji?.Trim();

if (string.IsNullOrWhiteSpace(addCommand) || string.IsNullOrWhiteSpace(removeCommand))
{
    Console.Error.WriteLine("Routing.AddCommand and Routing.RemoveCommand must both be configured.");
    return 2;
}

if (string.Equals(addCommand, removeCommand, StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Routing.AddCommand and Routing.RemoveCommand must be different.");
    return 2;
}

if (settings.ReactionRouting.Enabled &&
    (string.IsNullOrWhiteSpace(addReactionEmoji) || string.IsNullOrWhiteSpace(removeReactionEmoji)))
{
    Console.Error.WriteLine("ReactionRouting.AddEmoji and ReactionRouting.RemoveEmoji must both be configured when reaction routing is enabled.");
    return 2;
}

if (settings.ReactionRouting.Enabled &&
    string.Equals(
        NormalizeEmoji(addReactionEmoji!),
        NormalizeEmoji(removeReactionEmoji!),
        StringComparison.Ordinal))
{
    Console.Error.WriteLine("ReactionRouting.AddEmoji and ReactionRouting.RemoveEmoji must be different.");
    return 2;
}

if (!listFolders &&
    routingDestination == RoutingDestination.Folder &&
    string.IsNullOrWhiteSpace(folderName))
{
    Console.Write("Existing Telegram folder to use for /done: ");
    folderName = Console.ReadLine();
}

if (!listFolders &&
    routingDestination == RoutingDestination.Folder &&
    string.IsNullOrWhiteSpace(folderName))
{
    Console.Error.WriteLine("A target folder is required. Use --folder or JALIL_DONE_FOLDER.");
    return 2;
}

var dataDirectory = Environment.GetEnvironmentVariable("JALIL_DATA_DIR");
if (string.IsNullOrWhiteSpace(dataDirectory))
{
    dataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Jalil");
}

Directory.CreateDirectory(dataDirectory);
TryRestrictDirectoryPermissions(dataDirectory);

var sessionPath = Path.Combine(dataDirectory, "telegram.session");
var updateStatePath = Path.Combine(dataDirectory, "Updates.state");
var reactionStatePath = Path.Combine(dataDirectory, "Reactions.state.json");
var credentialsPath = Path.Combine(dataDirectory, "credentials.json");
var credentialsStore = new TelegramCredentialsStore(credentialsPath);
var login = new TelegramLoginConfiguration(
    sessionPath,
    settings.Telegram.PhoneNumber,
    credentialsStore,
    credentialsPath);
var options = new TelegramCompanionOptions(
    routingDestination,
    folderName?.Trim() ?? string.Empty,
    addCommand,
    removeCommand,
    settings.Behavior.DeleteCommandMessages,
    settings.ReactionRouting.Enabled,
    addReactionEmoji ?? string.Empty,
    removeReactionEmoji ?? string.Empty,
    sessionPath,
    updateStatePath,
    reactionStatePath,
    dryRun,
    listFolders);

Console.WriteLine($"Jalil data directory: {dataDirectory}");
Console.WriteLine($"Jalil settings file: {settingsPath}");
Console.WriteLine("Your Telegram login values are entered locally and are never sent to this chat.");

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

try
{
    await new TelegramCompanion(options, login.GetValue).RunAsync(shutdown.Token);
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Jalil stopped ({exception.GetType().Name}): {exception.Message}");
    if (string.Equals(
        Environment.GetEnvironmentVariable("JALIL_DIAGNOSTICS"),
        "1",
        StringComparison.Ordinal))
    {
        Console.Error.WriteLine(exception.StackTrace);
    }

    return 1;
}

static string? GetOption(string[] arguments, string name)
{
    for (var index = 0; index < arguments.Length; index++)
    {
        if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
        {
            return index + 1 < arguments.Length ? arguments[index + 1] : null;
        }
    }

    return null;
}

static string ResolveSettingsPath(string? explicitPath)
{
    if (!string.IsNullOrWhiteSpace(explicitPath))
    {
        return Path.GetFullPath(explicitPath);
    }

    var workingDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
    return File.Exists(workingDirectoryPath)
        ? workingDirectoryPath
        : Path.Combine(AppContext.BaseDirectory, "appsettings.json");
}

static string NormalizeEmoji(string emoji)
    => emoji.Replace("\uFE0F", string.Empty, StringComparison.Ordinal);

static void TryRestrictDirectoryPermissions(string path)
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        return;
    }

    try
    {
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    catch (Exception exception) when (exception is PlatformNotSupportedException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine("Warning: could not apply owner-only permissions to the Jalil data directory.");
    }
}

static void PrintHelp()
{
    Console.WriteLine(
        """
        Jalil - manage a Telegram conversation's native folder with commands and reactions

        Usage:
          Jalil [options]

        Options:
          --folder <name>   Existing Telegram folder to use
          --settings <path> Path to appsettings.json
          --list-folders    Sign in and list available Telegram folders
          --dry-run         Detect configured commands/reactions without making changes
          --help, -h        Show this help

        Environment variables:
          JALIL_TELEGRAM_API_ID
          JALIL_TELEGRAM_API_HASH
          JALIL_TELEGRAM_PHONE_NUMBER
          JALIL_DONE_FOLDER
          JALIL_SETTINGS_PATH
          JALIL_DATA_DIR (optional session/state directory)
          JALIL_DIAGNOSTICS=1 (include a stack trace when troubleshooting)
        """);
}

file sealed class TelegramLoginConfiguration(
    string sessionPath,
    string? configuredPhoneNumber,
    TelegramCredentialsStore credentialsStore,
    string credentialsPath)
{
    private bool _firstRunNoticeWritten;
    private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal)
    {
        ["session_pathname"] = sessionPath,
        ["device_model"] = Environment.MachineName,
        ["system_version"] = Environment.OSVersion.VersionString,
        ["app_version"] = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0"
    };

    public string? GetValue(string key)
    {
        if (_values.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var value = key switch
        {
            "api_id" => ResolveReusableValue(
                "JALIL_TELEGRAM_API_ID",
                credentialsStore.ApiId,
                () => ReadVisible("Telegram API ID: "),
                credentialsStore.SetApiId),
            "api_hash" => ResolveReusableValue(
                "JALIL_TELEGRAM_API_HASH",
                credentialsStore.ApiHash,
                () => ReadSecret("Telegram API hash: "),
                credentialsStore.SetApiHash),
            "phone_number" => ResolveReusableValue(
                "JALIL_TELEGRAM_PHONE_NUMBER",
                NullIfWhiteSpace(configuredPhoneNumber) ?? credentialsStore.PhoneNumber,
                () => ReadVisible("Telegram phone number (international format): "),
                credentialsStore.SetPhoneNumber),
            "verification_code" => ReadSecret("Telegram login code: "),
            "password" => ReadSecret("Telegram 2-step verification password: "),
            _ => null
        };

        if (key is not ("verification_code" or "password"))
        {
            _values[key] = value;
        }

        return value;
    }

    private string ResolveReusableValue(
        string environmentVariable,
        string? savedValue,
        Func<string> prompt,
        Action<string> save)
    {
        var environmentValue = NullIfWhiteSpace(Environment.GetEnvironmentVariable(environmentVariable));
        if (environmentValue is not null)
        {
            return environmentValue;
        }

        if (savedValue is not null)
        {
            return savedValue;
        }

        if (!_firstRunNoticeWritten)
        {
            Console.WriteLine("First-time Telegram setup. Reusable login values entered here are saved only on this computer.");
            Console.WriteLine($"Saved credentials file: {credentialsPath}");
            _firstRunNoticeWritten = true;
        }

        var value = prompt().Trim();
        if (!string.IsNullOrWhiteSpace(value))
        {
            save(value);
        }

        return value;
    }

    private static string ReadVisible(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ReadSecret(string prompt)
    {
        Console.Write(prompt);
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? string.Empty;
        }

        var buffer = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return new string([.. buffer]);
            }

            if (key.Key == ConsoleKey.Backspace && buffer.Count > 0)
            {
                buffer.RemoveAt(buffer.Count - 1);
                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                buffer.Add(key.KeyChar);
            }
        }
    }
}
