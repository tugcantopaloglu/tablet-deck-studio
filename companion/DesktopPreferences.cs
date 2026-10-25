using Microsoft.Win32;
using System.Text.Json;

namespace TabletDeck;

public sealed record DesktopSettings(bool UsbReconnect=false,string UsbSerial="",bool UpdateNotifications=false);
public sealed class DesktopPreferences
{
    private readonly string path;
    private readonly object gate=new();
    private DesktopSettings settings;
    public DesktopPreferences(string directory)
    {
        path=Path.Combine(directory,"desktop.json");
        settings=File.Exists(path)?JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(path),DataStore.Json)??new():new();
        Validate(settings);
    }
    public DesktopSettings Get(){lock(gate)return settings;}
    public void Save(DesktopSettings value)
    {
        Validate(value);
        lock(gate){var temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(value,DataStore.Json));File.Move(temp,path,true);settings=value;}
    }
    internal static void Validate(DesktopSettings value)
    {
        if(value.UsbSerial.Length>128||value.UsbSerial.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not ('.' or '_' or ':' or '-')))throw new ArgumentException("Enter the tablet serial shown by ADB, using letters, numbers, dots, underscores, colons or hyphens.");
    }
    private const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
    internal static string StartupCommand(string executable)
    {
        if(!Path.IsPathFullyQualified(executable)||executable.Contains('"'))throw new ArgumentException("Invalid companion executable path.");
        var command="\""+executable+"\" --tray --no-browser";
        if(command.Length>260)throw new ArgumentException("Install the companion at a shorter path before enabling startup.");
        return command;
    }
    public static bool StartsWithWindows()
    {
        using var key=Registry.CurrentUser.OpenSubKey(RunKey);return key?.GetValue("TabletDeckStudio") as string==StartupCommand(Environment.ProcessPath!);
    }
    public static void SetStartup(bool enabled)
    {
        using var key=Registry.CurrentUser.CreateSubKey(RunKey);
        if(enabled)key.SetValue("TabletDeckStudio",StartupCommand(Environment.ProcessPath!),RegistryValueKind.String);
        else key.DeleteValue("TabletDeckStudio",false);
    }
}
