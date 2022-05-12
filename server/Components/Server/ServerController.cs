
using System.Net;
using System.Net.Sockets;

namespace Server;

public class ServerController
{
    public ServerController()
    {
        Address = Host.AddressList[0];
        LocalEndPoint = new IPEndPoint(Address, 27550);
        Listener = new Socket(Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        Listener.Bind(LocalEndPoint);
        Listener.Listen(10);

        Logger.Info("Initialized", "Server");
        Logger.FLog($"server has started.");

        AddTitleInfo(() =>
        {
            return Program.Accepting ? "Running" : "Paused";
        });
    }

    public void SetPort(int port)
    {
        if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
        {
            Logger.Error($"cannot set a port to {port}, the value is not a valid registered port. (must be between {IPEndPoint.MinPort} & {IPEndPoint.MaxPort})");
            return;
        }

        LocalEndPoint!.Port = port;
    }
    public void Broadcast(string message)
    {
        foreach (var (_, sk) in Clients)
        {
            sk.Message($"{Program.ServerName}> {message}");
        }
    }

    public void BeginListen()
    {
        // Expect received data to contain a format string like this:
        // "Guid:Name"

        MainWatcher = new(x =>
        {
            for (; ; )
            {
                if (!Program.Accepting)
                {
                    Logger.Info($"This server is no longer accepting new connections.", "Server.BeginListen");
                    Broadcast("This server is now private & is not accepting new connections for now.");
                    SpinWait.SpinUntil(() => Program.Accepting);
                }
                var Connection = Listener!.Accept();
                var result = OnClientConnection(Connection);

                if (!result)
                    Connection.Dispose();
            }
        });
        MainWatcher.Start();
        TitleInformationManager = new Thread(OnTitleRefresh);
        TitleInformationManager?.Start();
    }

    ~ServerController()
    {
        TitleInformationManager?.Join();
        MainWatcher?.Join();
    }

    public IPHostEntry Host { get; set; } = Dns.GetHostEntry("localhost");
    public IPAddress? Address { get; set; } = null;
    public IPEndPoint? LocalEndPoint { get; set; } = null;
    public IDictionary<Client, Socket> Clients { get; set; } = new Dictionary<Client, Socket>();
    public Socket? Listener { get; set; } = null;
    private Thread? TitleInformationManager { get; set; }
    public List<Func<string>> TitleIdentifiers { get; } = new()
    {
    };
    public ServerStringPool SavedMessages { get; set; } = new();

    public void AddTitleInfo(Func<string> Evaluator) => TitleIdentifiers.Add(Evaluator);
    public void RemoveTitleInfo(Func<string> Evaluator) => TitleIdentifiers.Remove(Evaluator);

    public ParameterizedThreadStart OnTitleRefresh { get; set; } = delegate { };
    public Func<Socket, bool> OnClientConnection { get; set; } = delegate (Socket sock) { return false; };

    // arg0: The client, as in their identity.
    // arg1: The client socket, their connection.
    // arg2: The data supplied by the client. The first argument is always their Guid.
    // This is used as a comparison of the message & the client. If the client sends data
    // that doesn't start with a valid Guid, this callback will not be called and they will 
    // be disconnected.
    public Action<Client, Socket, string[]> OnMessageReceived { get; set; } = delegate { };

    private Thread? MainWatcher { get; set; }

    public static string GetString(byte[] data)
        => System.Text.Encoding.Default.GetString(data);

    public static byte[] GetBytes(string data)
        => System.Text.Encoding.Default.GetBytes(data);
}


