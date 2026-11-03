using System.Runtime.InteropServices;
using System.Text.Json;

namespace Jalil.App;

internal sealed class TelegramCredentialsStore
{
    private readonly string _path;
    private StoredCredentials _credentials;

    public TelegramCredentialsStore(string path)
    {
        _path = path;
        _credentials = Load(path);
    }

    public string? ApiId => NullIfWhiteSpace(_credentials.ApiId);

    public string? ApiHash => NullIfWhiteSpace(_credentials.ApiHash);

    public string? PhoneNumber => NullIfWhiteSpace(_credentials.PhoneNumber);

    public void SetApiId(string value)
    {
        _credentials = _credentials with { ApiId = value.Trim() };
        Save();
    }

    public void SetApiHash(string value)
    {
        _credentials = _credentials with { ApiHash = value.Trim() };
        Save();
    }

    public void SetPhoneNumber(string value)
    {
        _credentials = _credentials with { PhoneNumber = value.Trim() };
        Save();
    }

    private void Save()
    {
        var temporaryPath = $"{_path}.tmp";
        var json = JsonSerializer.Serialize(_credentials, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(temporaryPath, json);
        TryRestrictFilePermissions(temporaryPath);
        File.Move(temporaryPath, _path, overwrite: true);
        TryRestrictFilePermissions(_path);
    }

    private static StoredCredentials Load(string path)
    {
        if (!File.Exists(path))
        {
            return new StoredCredentials();
        }

        try
        {
            return JsonSerializer.Deserialize<StoredCredentials>(File.ReadAllText(path))
                   ?? new StoredCredentials();
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"The saved Telegram credentials file is invalid: {path}",
                exception);
        }
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void TryRestrictFilePermissions(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception exception) when (exception is PlatformNotSupportedException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Warning: could not apply owner-only permissions to the saved credentials file.");
        }
    }

    private sealed record StoredCredentials(
        string? ApiId = null,
        string? ApiHash = null,
        string? PhoneNumber = null);
}
