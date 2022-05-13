using Newtonsoft.Json;

namespace Server;

[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
public class Client : IDisposable
{
    private bool disposedValue;

    // Json specific default constructor
    [JsonConstructor]
    public Client() { }

    public Client(Guid id, string userName = "")
    {
        Logger.FLog($"loading user ({id})");

        Id = id;

        if (userName != "")
        {
            UserName = userName;
        }

        if (Directory.Exists($"{Directory.GetCurrentDirectory()}/users/{Id}"))
        {
            Logger.FLog($"loading saved user from {Directory.GetCurrentDirectory()}/users/{Id}");
            string filePath = $"{Directory.GetCurrentDirectory()}/users/{Id}/USER.JSON";

            var fileContents = File.ReadAllText(filePath);

            Client? savedClientInfo = JsonConvert.DeserializeObject<Client>(fileContents);

            if (savedClientInfo != null)
            {
                Logger.FLog($"users ({savedClientInfo.UserName}, {savedClientInfo.Id}) saved information loaded successfully, saving.");

                TimeMuted = savedClientInfo.TimeMuted;
                MuteReason = savedClientInfo.MuteReason;
                IsBanned = savedClientInfo.IsBanned;
                BanReason = savedClientInfo.BanReason;
            }

            if (IsMuted)
            {
                Logger.FLog($"{Id} is muted for {TimeMuted - DateTime.Now}");

                OnMutedThread = new Thread(MuteLoop);
                OnMutedThread.Start();
            }
        }
        else
        {
            Save();
        }
    }

    public void OnUserDisconnect()
    {
        Logger.FLog($"user {UserName} has disconnected.");
        Save();
    }

    [JsonProperty("user-id")]
    public Guid Id { get; set; }
    [JsonProperty("user-name")]
    public string UserName { get; set; }
    [JsonProperty("datetime-unmuted")]
    public DateTime TimeMuted { get; set; } = DateTime.MinValue;
    [JsonProperty("mute-reason")]
    public string MuteReason { get; set; } = string.Empty;
    [JsonProperty("is-banned")]
    public bool IsBanned { get; set; } = false;
    [JsonProperty("ban-reason")]
    public string BanReason { get; set; } = string.Empty;
    [JsonProperty("permissions")]
    public Permission Permissions { get; set; } = new(true);

    public bool IsMuted => DateTime.Now < TimeMuted;
    public void Mute(TimeSpan duration, string reason)
    {
        TimeMuted = DateTime.Now + duration;
        MuteReason = reason;

        Program.CommandManager.Execute("server.globalsay", new string[2] { "server.say", $"{UserName} has been muted for {reason} - duration: {duration.Days}d{duration.Minutes}m{duration.Seconds}s" }, Program.Server);
        Logger.Info($"Muted {UserName} for '{reason}', {duration.TotalSeconds}s");

        OnMutedThread = new Thread(MuteLoop);
        Save();
    }

    private Thread OnMutedThread { get; set; }
    public void Ban(string reason)
    {
        BanReason = reason;
        IsBanned = true;

        Program.CommandManager.Execute("server.globalsay", new string[2] { "server.say", $"{UserName} has been banned. Reason - {reason}" }, Program.Server);
        Logger.Info($"Successfully banned {UserName}");

        Save();
    }
    private void MuteLoop() => SpinWait.SpinUntil(() => !IsMuted);

    public static Client Create(Guid id, string userName) => new(id, userName);

    public TimeSpan ConnectionTime => TimeConnected - DateTime.Now;
    private DateTime TimeConnected { get; set; } = DateTime.Now;

    public void Save()
    {
        Directory.CreateDirectory($"{Directory.GetCurrentDirectory()}/users");
        Directory.CreateDirectory($"{Directory.GetCurrentDirectory()}/users/{Id}");

        var jsonResult = JsonConvert.SerializeObject(this, Formatting.Indented);

        if (jsonResult == string.Empty)
        {
            return;
        }

        try
        {
            Task.Run(async () => await File.WriteAllTextAsync($"{Directory.GetCurrentDirectory()}/users/{Id}/USER.JSON", jsonResult));
        }
        catch (Exception X) 
        {
            Logger.Error($"something caused us to be unable to save client '{UserName}'. [{X.Message}]");
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~Client()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}