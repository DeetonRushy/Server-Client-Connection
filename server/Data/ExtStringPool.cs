using Newtonsoft.Json;

namespace Server;

[Serializable]
public class ServerStringPool
{
    public IDictionary<string, string>? Pool { get; set; }
    public readonly string FilePath = $"{Directory.GetCurrentDirectory()}\\data\\strings\\messages.json";

    public ServerStringPool()
    {
        if (!File.Exists(FilePath))
        {
            throw new FileNotFoundException("a required file 'messages.json' was not found.");
        }

        var fileData = File.ReadAllText(FilePath);
        Pool = JsonConvert.DeserializeObject<IDictionary<string, string>>(fileData);

        new Thread(() =>
        {
            var FileInfo = new FileInfo(FilePath);

            while (true)
            {
                SpinWait.SpinUntil(() =>
                {
                    return FileInfo.Length != new FileInfo(FilePath).Length;
                });

                Update();
                FileInfo = new FileInfo(FilePath);
            }
        }).Start();

        Logger.FLog($"loaded {Pool!.Count} external string definitions");
    }

    public void Update()
    {
        Logger.FLog($"refreshing active string-pool definitions");

        var fileData = File.ReadAllText(FilePath);
        Pool = JsonConvert.DeserializeObject<IDictionary<string, string>>(fileData);
    }

    public string this[string key]
    {
        get
        {
            if (!Pool!.TryGetValue(key, out var value))
            {
                return string.Empty;
            }

            return value;
        }
    }
}
