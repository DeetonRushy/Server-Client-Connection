
namespace Server;

public static class Logger
{
    private static void Log(string data, string type, string caller)
    {
        Console.WriteLine($"({caller})[{TimeOnly.FromDateTime(DateTime.Now).ToLongTimeString()}][{type}] {data}");
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
}
