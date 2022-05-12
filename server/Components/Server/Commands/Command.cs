namespace Server;

public class Command
{
    public Command(Action<string[], ServerController> function, string desc)
    {
        Description = desc;
        Function = function;

        Logger.FLog($"command was created, meta info - '{desc}'");
    }

    public string Description { get; } = string.Empty;
    public Action<string[], ServerController> Function { get; set; } = delegate { };

    public static Command Create(Action<string[], ServerController> function, string desc)
        => new(function, desc);
}
