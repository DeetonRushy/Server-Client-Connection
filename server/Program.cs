using System.Security.Principal;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

// all sent data must begin with the users Guid, split with ':'.
// EX: Guid:Send:Hello There

namespace Server;

public class Program
{
    public static string? ServerName { get; set; } = "server";
    public static ServerCommandManager CommandManager { get; } = new ServerCommandManager();
    public static ServerController Server { get; set; }
    public static bool Accepting { get; set; } = true;
    public static int TotalMessagesReceived { get; set; } = 0;
    public static int MaxCapacity { get; set; } = 300;
    public static bool IsAdmin { get; set; } = false;
    public static string Version { get; } = "1.0.2-release.1";
    public static string Copyright
    {
        get
        {
            if (!Directory.Exists($"{Directory.GetCurrentDirectory()}\\info\\COPYRIGHT.TXT"))
            {
                return "No copyright file found.";
            }

            return File.ReadAllText($"{Directory.GetCurrentDirectory()}\\info\\COPYRIGHT.TXT");
        }
    }

    public static bool IsAdministrator()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void Main()
    {
        if (IsAdministrator())
        {
            IsAdmin = true;
        }

        if (!Directory.Exists($"{Directory.GetCurrentDirectory()}\\info\\COPYRIGHT.TXT"))
        {
            Console.WriteLine($"Hi there! Create a directory in the same location of this executable called 'info'.\nThen create a file called 'COPYRIGHT.txt' & enter my MIT License.");
        }

        ConsoleFont.SetConsoleFont();
        RegistryController.Initialize();

        Server =
            ServerBuilder.Create()
            .WithTitleCallback(x =>
            {
                TimeSpan RefreshInterval = TimeSpan.FromSeconds(10);

                while (true)
                {
                    string FormedTitle = $"Connected: {Server.Clients.Count}";
                    foreach (var item in Server.TitleIdentifiers)
                    {
                        FormedTitle += $" | {item()}";
                    }

                    Console.Title = FormedTitle;

                    Thread.Sleep(RefreshInterval);
                }
            })
            .WithOnMessageReceived((client, socket, data) =>
            {
                TotalMessagesReceived++;
                if (data[1] == "EXIT")
                {
                    Logger.Info($"{client.UserName} disconnected");
                    client.OnUserDisconnect();
                    Server.Clients[client].Dispose();
                    Server.Clients.Remove(client);
                    return;
                }

                if (!client.IsMuted)
                {
                    Console.WriteLine($"{client.UserName}: {data[1]}");
                }
                else
                {
                    Console.WriteLine($"{client.UserName}: {data[1]} (this message was not transmitted, they are muted for {client.TimeMuted})");
                }

                if (client.IsMuted)
                {
                    socket.Message($"You're currently muted & cannot send that message. (mute ends in {client.TimeMuted.TotalSeconds}s)");
                    return;
                }

                foreach (var (_, sock) in Server.Clients)
                {
                    sock.Message($"{client.UserName}> {data[1]}");
                }
            })
            .WithOnConnectionAccepted(client =>
            {
                if (Server.Clients.Count >= MaxCapacity)
                {
                    client.Send("400: server is full.".Bytes());
                    client.Close();
                    return false;
                }

                // initial data, not sure how to optimize size allocated.
                byte[] data = new byte[256];

                var lengthOfReceivedData = client.Receive(data);
                var readableData = data.String();

                var receivedData = readableData.Replace("\0", "").Split(':');

                if (receivedData.Length != 2)
                {
                    client.Send("401: authentication failed.".Bytes());
                    client.Close();
                    return false;
                }

                if (!Guid.TryParse(receivedData[0], out var guid))
                {
                    client.Send("401: authentication failed.".Bytes());
                    client.Close();
                    return false;
                }

                if (Server.Clients.UserConnected(guid))
                {
                    client.Message($"87: Only one client can connect at a time.");
                    Logger.Warning($"Declined connection from {guid}. They're connected on another client. ({Server.Clients.Where(x => x.Key.Id == guid).First().Key.UserName})");
                    return false;
                }

                var clientData = Client.Create(guid, receivedData[1].Trim());

                if (clientData.IsBanned)
                {
                    client.Message($"You've been permanently banned from this server for {clientData.BanReason}");
                    client.Message($"client.close:Now");
                    return false;
                }

                Server.Clients.Add(clientData, client);

                new Thread(() =>
                {
                    while (client.Connected)
                    {
                        SpinWait.SpinUntil(() =>
                        {
                            try
                            {
                                return client.Available > 0;
                            }
                            catch
                            {
                                return true;
                            }
                        });

                        try
                        {
                            var IsDisposed = client.Available;
                        }
                        catch (ObjectDisposedException)
                        {
                            break;
                        }

                        byte[] received = new byte[client.Available];
                        client.Receive(received);

                        var data = received.Splice(':');

                        if (!Guid.TryParse(data[0], out var guid))
                        {
                            client.Send("401: authentication failed.".Bytes());
                            client.Close();
                            continue;
                        }

                        var (savedClient, socket) = Server.Clients.ContainsClient(guid);
                        if (savedClient == null || socket == null)
                        {
                            client.Send("202: your connection was rejected.".Bytes());
                            client.Close();
                            continue;
                        }

                        Server.OnMessageReceived(savedClient, socket, data);
                    }

                    Logger.Info($"WorkerThread for {guid} stopped due to socket closing.");

                }).Start();
                Logger.Info($"{clientData.UserName}({clientData.Id}) has connected.");
                return true;
            })
            .WithPort(int.Parse(RegistryController.Read("ServerPort")!.ToString()))
            .WithTitleInfo(() => $"MessagesReceivedTotal: {TotalMessagesReceived}")
            .WithTitleInfo(() => "server.help for command info")
            .WithTitleInfo(() => IsAdmin ? "Administrator" : "User")
            .Build();

        Server.BeginListen();

        while (true)
        {
            ServerName = (string?)RegistryController.Read("ServerName");

            Console.Write($"{ServerName}> ");
            var ConsoleResponse = Console.ReadLine();

            if (ConsoleResponse == string.Empty)
            {
                Console.Clear(); // => This is what I usually want when I start entering nothing lol.
                continue;
            }

            var (name, args) = ServerCommandManager.DisectArgs(ConsoleResponse!.ToLower());

            CommandManager.Execute(name, args, Server);
        }
    }
}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
