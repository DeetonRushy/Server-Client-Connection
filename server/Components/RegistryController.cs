using Microsoft.Win32;

namespace Server;

public static class RegistryController
{
    public const string RegKey = @"SOFTWARE\ADDTServerSettings";
    public static Dictionary<string, object> DefaultRegValues { get; } = new()
    {
        { "ServerName", "server" },
        { "ServerPort", 27550 },
        { "ServerOwner", "<redacted>" },
        { "ServerEmail", "<redacted>" },
        { "MaxCapacity", 300 }
    };

    public static void Initialize()
    {
        Logger.Info($"Initialized {DefaultRegValues.Count} values", "RegistryController");

        var regKey = Registry.CurrentUser.CreateSubKey(RegKey);
        var regKeyKeys = regKey.GetValueNames().Skip(1); // ignore (default)

        if (DefaultRegValues.Count == regKeyKeys.Count())
            return;

        foreach (var (key, value) in DefaultRegValues)
        {
            regKey.SetValue(key, value);
        }
    }

    public static object? Read(string key)
    {
        var regKey = Open();
        var value = regKey.GetValue(key);

        if (value == null)
            throw new ArgumentException($"{key} doesn't exist in our subkey.");

        regKey?.Close();
        return value;
    }

    public static void Write(string key, object value)
    {
        var regKey = Open();
        regKey?.SetValue(key, value);
        regKey.Close();
    }

    private static RegistryKey? Open()
        => Registry.CurrentUser.CreateSubKey(RegKey);
}
