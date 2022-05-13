using System.Diagnostics;
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
            if (!Directory.Exists($"{Directory.GetCurrentDirectory()}\\data\\licenses\\LICENSE.txt"))
            {
                return "No copyright file found.";
            }

            return File.ReadAllText($"{Directory.GetCurrentDirectory()}\\data\\licenses\\LICENSE.txt");
        }
    }

    public static string DefinitionOf(string str) => Server.Fetch(str);
    public static ClientCommandHandler ClientCommandHandler { get; set; } = new();

    public static bool IsAdministrator()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);

        Logger.FLog($"IsAdmin: {principal.IsInRole(WindowsBuiltInRole.Administrator)}");

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
                    client.Send("the server didn't get the expected response and was forced to disconnect you.".Bytes());
                    client.Close();
                    return false;
                }

                if (!Guid.TryParse(receivedData[0], out var guid))
                {
                    client.Send("the client response contained invalid data that could not be parsed.".Bytes());
                    client.Close();
                    return false;
                }

                if (Server.Clients.UserConnected(guid))
                {
                    client.Message($"you're already connected from this account.");
                    Logger.Warning($"Declined connection from {guid}. They're connected on another client. ({Server.Clients.Where(x => x.Key.Id == guid).First().Key.UserName})");
                    return false;
                }

                var clientData = Client.Create(guid, receivedData[1].Trim());

                if (clientData.IsBanned)
                {
                    client.Message(string.Format(DefinitionOf("SV_BAN_DETECTED_01"), clientData.BanReason));
                    client.Message($"client.close:Now");
                    return false;
                }

                Server.Clients.Add(clientData, client);
                client.Message(DefinitionOf("CL_HTU_COMMANDS_HELP_01") + "\n");
                client.Message("\n" + DefinitionOf("MISC_GITHUB_LINK") + "\n");

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
                            client.Send(DefinitionOf("SV_CONN_INVALID_RESPONSE_01").Bytes());
                            client.Close();
                            continue;
                        }

                        var (savedClient, socket) = Server.Clients.ContainsClient(guid);
                        if (savedClient == null || socket == null)
                        {
                            client.Send(DefinitionOf("SV_CONN_UNKNOWN_SENDER_01").Bytes());
                            client.Close();
                            continue;
                        }

                        ClientCommandHandler.Handle(received.String(), Server);
                    }

                    Logger.Info($"WorkerThread for {guid} stopped due to socket closing.");

                }).Start();
                Logger.Info($"{clientData.UserName}({clientData.Id}) has connected.");
                client.Message($"{CommandManager.ServerStorage.Fetch("motd")}");
                return true;
            })
            .WithPort(int.Parse(RegistryController.Read("ServerPort")!.ToString()))
            .WithTitleInfo(() => $"MessagesReceivedTotal: {TotalMessagesReceived}")
            .WithTitleInfo(() => "server.help for command info")
            .WithTitleInfo(() => IsAdmin ? "Administrator" : "User")
            .Build();

        Server.BeginListen();
        // Wait for initial information to be sent.
        Thread.Sleep(TimeSpan.FromSeconds(2));

        while (true)
        {
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
