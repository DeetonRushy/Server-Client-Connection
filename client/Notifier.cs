using Microsoft.Win32;
using System.Diagnostics;

namespace Client;

public enum ToastDuration
{
    Short,
    Long
}

public static class Notifier
{
    private static readonly string ScriptPath = $"{Directory.GetCurrentDirectory()}\\data\\scripts\\notifs.py";
    private static bool HasPythonInstalled { get; set; } = true;
    private static bool HasVerifiedModules { get; set; } = false;

    private static bool VerifyState()
    {
        RegistryKey? key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ADDTClientSettings");
        var HasDonePrompt = key.GetValue("HasDonePythonModulePrompt");

        if (HasDonePrompt == null)
            HasVerifiedModules = false;
        else
        {
            HasVerifiedModules = bool.Parse((string)HasDonePrompt);
        }

        if (!File.Exists(ScriptPath))
        {
            Logger.FLog($"unable to create toast as the script doesn't exist");
            return false;
        }

        return HasPythonInstalled;
    }

    public static void Show(string title, string message, ToastDuration duration = ToastDuration.Short)
    {
        if (!VerifyState())
            return;

        string dur = duration switch
        {
            ToastDuration.Long => "long",
            ToastDuration.Short => "short",
            _ => "short"
        };

        ProcessStartInfo info = new()
        {
            FileName = "python.exe",
            Arguments = $"{ScriptPath} --app_id \"DClient\" --title \"{title}\" --msg \"{message}\" --dur \"{dur}\""
        };

        try
        {
            Process.Start(info);
        }
        catch (Exception)
        {
            HasPythonInstalled = false;
        }

        if (!HasVerifiedModules)
        {
            Console.Clear();

            Console.WriteLine("-- YOU WILL ONLY BE ASKED THIS ONCE --");
            Console.Write($"Can we verify all required python modules exist? This will only download 1 item. (y/n) ");
            var Result = Console.ReadLine();

            if (Result.ToLower().Contains('y'))
            {
                try
                {
                    Process.Start("python.exe", "-m pip install --upgrade pip").WaitForExit();
                    Process.Start("python.exe", "-m pip install winotify").WaitForExit();
                }
                catch { }

                Console.WriteLine($"\nThanks, if you have python installed you'll see cool toast notifications now.");
            }
            else
            {
                Console.WriteLine($"\nNo problem!");
            }

            RegistryKey? key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ADDTClientSettings");
            key.SetValue("HasDonePythonModulePrompt", true);
        }

        HasPythonInstalled = true;
    }
}
