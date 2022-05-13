
namespace Client;

public static class Logger
{
    private static void Log(string data, string type, string caller)
    {
        Task.Run(async () =>
        {
            await Console.Out.WriteLineAsync($"({caller})[{TimeOnly.FromDateTime(DateTime.Now).ToLongTimeString()}][{type}] {data}");
        });
    }

    public static void Info(string information, string caller = "Unknown")
    {
        Log(information, "Info", caller);
    }

    public static void Error(string information, string caller = "Unknown")
    {
        Log(information, "Error", caller);
    }

    public static void Warning(string information, string caller = "Unknown")
    {
        Log(information, "Warning", caller);
    }

    public static void Fatal(string information, string caller)
    {
        Log(information, "Fatal", caller);
    }

    private static bool NewSession = true;

    public static void FLog(string information)
    {
        const string FileName = "debug.log";

        if (!File.Exists(FileName))
        {
            File.Create(FileName).Close();
            File.AppendAllText(FileName, $"NEW SESSION ~0x{Directory.GetCurrentDirectory().GetHashCode():X2}");
        }

        if (NewSession)
        {
            File.AppendAllText(FileName, $"\n\nNEW SESSION ~0x{Directory.GetCurrentDirectory().GetHashCode():X2}\n\n");
            NewSession = false;
        }

        File.AppendAllText(FileName, $"[{TimeOnly.FromDateTime(DateTime.Now).ToLongTimeString()}][Log] {information}\n");
    }
}
