using System.Text.Json;

namespace Jalil.App;

internal sealed class JalilAppSettings
{
    public TelegramSettings Telegram { get; init; } = new();

    public RoutingSettings Routing { get; init; } = new();

    public BehaviorSettings Behavior { get; init; } = new();

    public static JalilAppSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Settings file was not found: {path}", path);
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<JalilAppSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        }) ?? throw new InvalidOperationException("The settings file is empty or invalid.");
    }
}
internal sealed class TelegramSettings
{
    public string? PhoneNumber { get; init; }
}

internal sealed class RoutingSettings
{
    public string FolderName { get; init; } = "test";

    public string AddCommand { get; init; } = "/done";

    public string RemoveCommand { get; init; } = "/undo";
}

internal sealed class BehaviorSettings
{
    public bool DeleteCommandMessages { get; init; } = true;
}
