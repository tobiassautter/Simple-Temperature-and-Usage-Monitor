using System.IO;
using System.Text.Json;

namespace TempMonitor;

public sealed class Settings
{
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public bool ClickThrough { get; set; }
    public double Opacity { get; set; } = 0.85;
    public int UpdateIntervalMs { get; set; } = 1000;

    private static string PathFor() => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TempMonitor", "settings.json");

    public static Settings Load()
    {
        try
        {
            var path = PathFor();
            if (File.Exists(path))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new Settings();
        }
        catch { /* corrupt settings -> defaults */ }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            var path = PathFor();
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this));
        }
        catch { /* non-fatal */ }
    }
}
