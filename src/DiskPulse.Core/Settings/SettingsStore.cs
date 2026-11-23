using System.Text.Json;

namespace DiskPulse.Core.Settings;

/// <summary>Loads and saves <see cref="AppSettings"/> as JSON. Never throws on a bad or missing file.</summary>
public sealed class SettingsStore(string path)
{
    /// <summary>Anything bigger is not a settings file DiskPulse wrote, so it is not parsed.</summary>
    public const long MaxFileBytes = 1024 * 1024;

    public static string DefaultPath { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiskPulse", "settings.json");

    public string Path { get; } = path;

    /// <summary>Where an unreadable settings file is copied before defaults replace it.</summary>
    public string InvalidCopyPath => System.IO.Path.ChangeExtension(Path, ".invalid.json");

    /// <summary>
    /// Returns the saved settings, normalized. A missing file gives defaults. A damaged file gives
    /// defaults too, after copying it to <see cref="InvalidCopyPath"/> so the user can recover it.
    /// </summary>
    public AppSettings Load()
    {
        AppSettings? settings = null;
        try
        {
            var file = new FileInfo(Path);
            if (file.Exists)
            {
                if (file.Length > MaxFileBytes)
                    throw new JsonException("Settings file is too large.");

                using var stream = file.OpenRead();
                settings = JsonSerializer.Deserialize<AppSettings>(stream, SettingsJson.Options);
            }
        }
        catch (JsonException)
        {
            KeepInvalidCopy();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable right now (locked, no permission). Defaults for this run; nothing is overwritten.
        }

        settings ??= new AppSettings();
        settings.Normalize();
        return settings;
    }

    /// <summary>Writes atomically: a crash mid-save leaves the previous file intact.</summary>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        string temp = Path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, SettingsJson.Options));
        File.Move(temp, Path, overwrite: true);
    }

    private void KeepInvalidCopy()
    {
        try
        {
            File.Copy(Path, InvalidCopyPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
