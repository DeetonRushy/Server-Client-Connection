using Newtonsoft.Json;

namespace Server;

[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
public class Client : IDisposable
{
    private bool disposedValue;

    // Json specific default constructor
    public Client() { }

    public Client(Guid id, string userName)
    {
        Id = id;
        UserName = userName;

        if (Directory.Exists($"{Directory.GetCurrentDirectory()}/users/{Id}"))
        {
            string filePath = $"{Directory.GetCurrentDirectory()}/users/{Id}/USER.JSON";

            var fileContents = File.ReadAllText(filePath);

            Client? savedClientInfo = JsonConvert.DeserializeObject<Client>(fileContents);

            if (savedClientInfo != null)
            {
                TimeMuted = savedClientInfo.TimeMuted;
                MuteReason = savedClientInfo.MuteReason;
                IsBanned = savedClientInfo.IsBanned;
                BanReason = savedClientInfo.BanReason;
            }

            if (TimeMuted > TimeSpan.Zero)
            {
                OnMutedThread = new Thread(MuteLoop);
                OnMutedThread.Start();
            }
        }
    }

    public void OnUserDisconnect()
    {
        Save();
    }

    [JsonProperty("user-id")]
    public Guid Id { get; set; }
    [JsonProperty("user-name")]
    public string UserName { get; set; }
    [JsonProperty("mute-duration")]
    public TimeSpan TimeMuted { get; set; } = TimeSpan.FromSeconds(0);
    [JsonProperty("mute-reason")]
    public string MuteReason { get; set; } = string.Empty;
    [JsonProperty("is-banned")]
    public bool IsBanned { get; set; } = false;
    [JsonProperty("ban-reason")]
    public string BanReason { get; set; } = string.Empty;

    public bool IsMuted => (TimeMuted > TimeSpan.Zero);
    public void Mute(TimeSpan duration, string reason)
    {
        TimeMuted = duration;
        MuteReason = reason;

        Program.CommandManager.Execute("server.globalsay", new string[2] { "server.say", $"{UserName} has been muted for {reason} - duration: {duration.Days}d{duration.Minutes}m{duration.Seconds}s" }, Program.Server);
        Logger.Info($"Muted {UserName} for '{reason}', {duration.TotalSeconds}s");

        OnMutedThread = new Thread(MuteLoop);
        OnMutedThread.Start();
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
    private void MuteLoop()
    {
    RestartMute:
        while (IsMuted)
        {
            var TimeSpanToSleepFor = TimeSpan.FromSeconds(2);
            Thread.Sleep(TimeSpanToSleepFor);
            TimeMuted -= TimeSpanToSleepFor;
            var AvailableClient = Program.Server.Clients.Where(x => x.Key.Id == Id);

            // No escaping mutes by disconnecting, mutes must be served through online time.
            if (AvailableClient.Count() == 0)
                break;

            AvailableClient.First().Value.Message($"client.settitle:You've been muted. {TimeMuted.Minutes}m{TimeMuted.Seconds}s left");

            Save();
        }

        SpinWait.SpinUntil(() => IsMuted);
        goto RestartMute;
    }

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
            File.WriteAllText($"{Directory.GetCurrentDirectory()}/users/{Id}/USER.JSON", jsonResult);
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