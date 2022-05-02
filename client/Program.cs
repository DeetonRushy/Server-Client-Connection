using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace Client;

using BOOL = Boolean;

public class TraceDataHandler
{
    public TraceDataHandler() 
    {
        var RegKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\ADDTClientSettings");

        if (RegKey is null)
        {
            // create key, this is a first time user.
            RegKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ADDTClientSettings");
            RegKey.SetValue("GlobalGuid", Guid.NewGuid().ToString());
            RegKey.SetValue("IsBanned", false);
            RegKey.SetValue("BanReason", string.Empty);

            Console.Write("We see you're a new user, what name would you like to use?: ");
            var UserName = Console.ReadLine()!.Normalize()!.Trim();
            RegKey.SetValue("UserName", UserName);
        }

        IsBanned = bool.Parse((string?)RegKey!.GetValue("IsBanned"));
        
        if (!Guid.TryParse((string?)RegKey.GetValue("GlobalGuid"), out Guid globalUid))
        {
            Tamper();
        }
        if (IsBanned == false && (string?)RegKey.GetValue("BanReason") != string.Empty)
        {
            Tamper();
        }

        GlobalGuid = globalUid;
        UserName = (string?)RegKey.GetValue("UserName");

        if (IsBanned == true)
        {
            Console.WriteLine("You are currently banned with the reason:\n");
            Console.WriteLine(RegKey.GetValue("BanReason"));
            Console.WriteLine("Press any key to exit..");
            Console.ReadKey();
            Environment.Exit(0);
        }

        RegKey.Close();
    }

    public void BanUser(string reason)
    {
        var RegKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ADDTClientSettings");
        RegKey.SetValue("IsBanned", true);
        RegKey.SetValue("BanReason", reason);

        Console.WriteLine($"You've been banned from DDTChat, with the reason: {reason}");
        Console.WriteLine("Press any key to exit...");
        Environment.Exit(0);
    }
    public void Tamper()
    {
        Console.WriteLine("You've tampered with critical data needed for this client to function. Please press any key to exit.");
        BanUser("tampering with critical data with malicious intent.");
        Console.ReadKey();
        Environment.Exit(0);
    }

    public BOOL? IsBanned { get; set; } = false;
    public Guid? GlobalGuid { get; set; }
    public string? UserName { get; set; }
}
public class Client
{
    public Client()
    {
        ClientStatus = new();
        UserName = ClientStatus.UserName;
        Id = ClientStatus.GlobalGuid;


        // at this point, if the local user is banned, they are gone.

        Address = Host.AddressList[0];
        RemoteEndPoint = new IPEndPoint(Address, 27550);
        Sender = new Socket(Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        int attempt = 1;
RetryConnection:
        if (attempt >= 10)
        {
            Console.WriteLine($"Failed to connect to the server after {attempt} retries. Please try again later.");
            Thread.Sleep(TimeSpan.FromSeconds(3));
            Environment.Exit(0);
        }

        try
        {
            Sender.Connect(RemoteEndPoint);
        }
        catch (Exception)
        {
            Console.WriteLine($"Failed to connect to the server. Attempt={attempt}");
            attempt++;
            goto RetryConnection;
        }
        // Initial message to introduce ourselves
        Sender.Send(GetBytes($"{Id}:{UserName}"));

        var initialResponse = Receive();

        if (initialResponse.Contains("390"))
        {
            throw new ArgumentException("server denied connection due to supplied name being too long. (must be <= 15)");
        }
    }

    public void Send(string data)
    {
        Sender.Send(GetBytes($"{Id}:{data}"));
    }
    public string Receive()
    {
        if (Sender.Available > 0)
        {
            byte[] recBytes = new byte[256];
            Sender.Receive(recBytes);
            return GetString(recBytes);
        }
        return string.Empty;
    }

    public IPHostEntry Host { get; } = Dns.GetHostEntry("localhost");
    public IPAddress Address { get; }
    public IPEndPoint RemoteEndPoint { get; }
    public Socket Sender { get; }
    public Guid? Id { get; } = Guid.NewGuid();
    public string? UserName { get; }
    public TraceDataHandler ClientStatus { get; set; }

    public static string GetString(byte[] data)
    => Encoding.Default.GetString(data);

    public static byte[] GetBytes(string data)
        => Encoding.Default.GetBytes(data);
}
public static class ExternalFunctions
{
    [DllImport("Kernel32")]
    public static extern bool SetConsoleCtrlHandler(HandlerRoutine handlerRoutine, bool add);
    public delegate bool HandlerRoutine(int signal);
}
public class ServerActionHandler 
{
    public ServerActionHandler() 
    {
        Add("client.printlocaldata", args =>
        {
            Console.WriteLine( $"Current Port: {Program.Client.RemoteEndPoint.Port}" );
        });

        Add("client.settitle", args =>
        {
            Console.Title = $"{args}";
        });

        Add("client.close", args =>
        {
            Thread.Sleep(TimeSpan.FromSeconds(5.0));
        });
    }

    public void Add(string name, Action<string> callback)
    {
        ServerActions.Add(name, callback);
    }

    public bool Handle(string res)
    {
        // We expect the server to send actions like this - ActionName:Args:Split:With:':'

        if (!res.Contains(':'))
            return false;

        var Command = res.Split(':')[0];
        var Args = res.Split(':')[1];

        if (!ServerActions.ContainsKey(Command))
            return false;

        var Action = ServerActions[Command];

        Action(Args);
        return true;
    }

    public Dictionary<string, Action<string>> ServerActions { get; } = new();
}

public static class Program
{
    public static Client Client { get; } = new();
    public static ServerActionHandler ActionHandler { get; } = new();

    public static void Main()
    {
        ExternalFunctions.SetConsoleCtrlHandler(signal =>
        {
            if (signal == 2)
            {
                Client.Send("EXIT");
                return true;
            }

            return false;
        }, true);

        new Thread(() =>
        {
            while (true)
            {
                SpinWait.SpinUntil(() => Client.Sender.Available > 0);

                byte[] arr = new byte[Client.Sender.Available];
                Client.Sender.Receive(arr);

                var response = Client.GetString(arr).Trim();

                if (ActionHandler.Handle(response))
                {
                    continue;
                }

                Console.WriteLine($"{response}");
            }

        }).Start();

        while (true)
        {
            Console.Write($"{Client.UserName}> ");
            var Message = Console.ReadLine()!.Trim();
            Console.WriteLine();

            if (Message is "")
                continue;

            if (Message == "exit")
            {
                if (Client.Sender.Connected)
                    Client.Send($"EXIT");
                Thread.Sleep(TimeSpan.FromSeconds(3));
                Environment.Exit(0);
            }

            Client.Send(Message!.lower()!.Normalize()!.TrimEnd());
            Thread.Sleep(TimeSpan.FromSeconds(1));
        }
    }
}

public static class Extensions
{
    public static string lower(this string str) => str.ToLower();
}
