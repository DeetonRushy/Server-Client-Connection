using System.Net;
using Newtonsoft.Json;

namespace Server;

[JsonObject(MemberSerialization.OptIn)]
public class Storage
{
    const string Empty = "";

    [JsonProperty("storage-current", NullValueHandling = NullValueHandling.Ignore)]
    public IDictionary<string, string> _storage { get; set; }

    public Storage()
    {
        _storage = new Dictionary<string, string>();
    }

    public void Save(string path)
    {
        var Converted = JsonConvert.SerializeObject(this, Formatting.Indented);

        Task.Run(async () => await File.Create(path).DisposeAsync());
        Task.Run(async () => await File.WriteAllTextAsync(path, Converted));
    }

    public void Load(string path)
    {
        // Most likely not been saved yet, np.
        if (!File.Exists(path))
            return;

        var Converted = JsonConvert.DeserializeObject<Storage>(File.ReadAllText(path));
        _storage = Converted._storage;

        Logger.FLog($"loaded saved data from '{path}'");
    }

    public void Insert(string Name, string Value)
    {
        if (_storage.ContainsKey(Name))
            return;

        Logger.FLog($"[storage] saved {{ {Name}: {Value} }}");

        _storage.Add(Name, Value);
    }
    public bool Query(string name)
    {
        return _storage.ContainsKey(name);
    }
    public string Fetch(string name)
    {
        if (!_storage.ContainsKey(name))
            return Empty;
        return _storage[name];
    }
    public void Set(string name, string value)
    {
        if (!Query(name))
            return;

        _storage[name] = value;
    }

    /// <summary>
    /// Input: "Hello, {StorageVariableName}"
    /// Output: "Hello, StorageVariableValue"
    /// </summary>
    /// <param name="format"></param>
    /// <returns>the formated string, containing the values instead of names.</returns>
    public string Format(string format)
    {
        string Result = string.Empty;

        for (int i = 0; i < format.Length; i++)
        {
            string ctx = string.Empty;

            if (format[i] != '{')
            {
                Result += format[i];
                continue;
            }

            while (format[++i] != '}')
                ctx += format[i];

            if (!Query(ctx))
                throw new FormatException($"failed to format string, variable {ctx} is not recognized");

            var value = Fetch(ctx);

            Result += value;
        }

        return Result;
    }
}

public class ServerCommandManager
{
    public static string Owner { get; set; } = string.Empty;
    public static string EMail { get; set; } = string.Empty;
    public Storage ClientStorage = new();
    public Storage ServerStorage = new();

    readonly string ClientStoragePath = $"{Directory.GetCurrentDirectory()}\\data\\client-storage.json";
    readonly string ServerStoragePath = $"{Directory.GetCurrentDirectory()}\\data\\server-storage.json";

    public ServerCommandManager()
    {
        ServerStorage.Insert("motd", $"Welcome to {Program.ServerName}");

        ClientStorage.Save(ClientStoragePath);
        ServerStorage.Save(ServerStoragePath);

        // Initialize default commands.
        Commands = new Dictionary<string, Command>();

        /*
         args[0] is always the command name.
         */

        /*
         * As Below: args[0] = "visual.name"
        */

        // namespace Visual;
        Add("visual.name", (args, server) =>
        {
            if (args.Length != 2)
            {
                Logger.Warning("invalid arguments", "visual.name");
                return;
            }

            var newServerName = args[1];

            RegistryController.Write("ServerName", newServerName);
            Program.ServerName = newServerName;
        }, "changes the default server name that clients will see.");
        Add("visual.font", (args, server) =>
        {
            if (args.Length != 2)
            {
                Logger.Warning("usage: visual.font <font-name>");
                return;
            }

            var Font = args[1];
            ConsoleFont.SetConsoleFont(Font);

        }, "Set the server font.");

        // namespace Server;
        Add("server.help", (args, server) =>
        {
            if (args.Length == 1)
            {
                Console.WriteLine();
                Commands.ToList().ForEach(command => Console.WriteLine($"{command.Key}: {command.Value.Description}"));
                Console.WriteLine();
                return;
            }

            if (args.Length == 2 && Commands.ContainsKey(args[1]))
            {
                Console.WriteLine($"{args[1]}: {Commands[args[1]].Description}");
            }
        }, "display help for all commands, or supply a command name for a specific command.");
        Add("server.port", (args, server) =>
        {
            if (args[0] != "server.port")
            {
                Logger.Error($"server.port was called, but command was {args[0]}. (report this to developers)");
                return;
            }

            if (args.Length == 1)
            {
                Logger.Info($"server.port: {RegistryController.Read("ServerPort")}");
                return;
            }

            var IntValue = int.TryParse(args[1], out var value);
            if (!IntValue)
            {
                Logger.Warning("server.port must be a number.");
                return;
            }

            RegistryController.Write("ServerPort", value);

        }, "Set the server port.");
        Add("server.send", (args, server) =>
        {
            if (args[0] != "server.send")
            {
                Logger.Error($"server.send was called, but command was {args[0]}. (report this to developers)");
                return;
            }
            if (args.Length < 3)
            {
                Logger.Warning("usage: ", "server.send <user-id> <action>");
                return;
            }

            var Uid = Guid.TryParse(args[1], out var value);

            if (!Uid)
            {
                Logger.Warning($"the user-id isn't a valid Guid.", "server.send");
                return;
            }

            var allUsersWithUid = server.Clients.Where(x => x.Key.Id == value).ToList();

            if (allUsersWithUid.Count() == 0)
            {
                Logger.Warning($"no user with that Guid exists.");
                return;
            }

            var clientCommand = args[2];
            var clientArgumentsMut = args.Skip(3);
            var clientArgumentsToSend = string.Empty;

            for (int i = 0; i < clientArgumentsMut.Count(); i++)
            {
                if (i == clientArgumentsMut.Count())
                {
                    clientArgumentsToSend += $"{clientArgumentsMut.ElementAt(i)}";
                }
                else
                {
                    clientArgumentsToSend += $"{clientArgumentsMut.ElementAt(i)}:";
                }
            }

            foreach (var client in allUsersWithUid)
            {
                client.Value.Message($"{clientCommand}:{clientArgumentsToSend}");
            }

        }, "Send an action to be executed on the client. usage: server.send <user-id> <action>. actions: check documentation.");
        Add("server.clients", (_, server) =>
        {
            if (server.Clients.Count == 0)
            {
                Logger.Info("there is nobody connected", "server.clients");
                return;
            }

            foreach (var (client, socket) in server.Clients)
            {
                var remoteIPEndPoint = socket.RemoteEndPoint as IPEndPoint;

                if (remoteIPEndPoint != null)
                {
                    Console.WriteLine($"{client.UserName}({client.Id}) - Ip: {remoteIPEndPoint.Address}:{remoteIPEndPoint.Port} (remote), (Muted: {client.IsMuted}, MuteLength: {client.TimeMuted}) ({client.ConnectionTime})");
                    continue;
                }

                var localIPEndPoint = socket.LocalEndPoint as IPEndPoint;

                if (localIPEndPoint != null)
                {
                    Console.WriteLine($"{client.UserName}({client.Id}) - Ip: {localIPEndPoint.Address}:{localIPEndPoint.Port} (local), (Muted: {client.IsMuted}, MuteLength: {client.TimeMuted}) ({client.ConnectionTime})");
                    continue;
                }

                Console.WriteLine($"{client.UserName}({client.Id}) - Ip: Unable to get network information.");
            }

        }, "List all current clients Names, Ids, Network information.");
        Add("server.mute", (args, server) =>
        {
            if (args.Length < 3)
            {
                Logger.Warning($"usage: server.mute <user-id> <duration> [reason]");
                return;
            }

            TimeSpan ParseTimeString(string str)
            {
                if (str.Contains('s'))
                {
                    str = str.Replace("s", "");
                    if (!int.TryParse(str, out int seconds))
                    {
                        Logger.Warning($"'{str}' is not a valid TimeSpan.");
                        return TimeSpan.Zero;
                    }

                    return TimeSpan.FromSeconds(seconds);
                }
                if (str.Contains('m'))
                {
                    str = str.Replace("m", "");
                    if (!int.TryParse(str, out int minutes))
                    {
                        Logger.Warning($"'{str}' is not a valid TimeSpan.");
                        return TimeSpan.Zero;
                    }

                    return TimeSpan.FromMinutes(minutes);
                }
                if (str.Contains('h'))
                {
                    str = str.Replace("h", "");
                    if (!int.TryParse(str, out int hours))
                    {
                        Logger.Warning($"'{str}' is not a valid TimeSpan.");
                        return TimeSpan.Zero;
                    }

                    return TimeSpan.FromHours(hours);
                }
                if (str.Contains('d'))
                {
                    str = str.Replace("d", "");
                    if (!int.TryParse(str, out int days))
                    {
                        Logger.Warning($"'{str}' is not a valid TimeSpan.");
                        return TimeSpan.Zero;
                    }

                    return TimeSpan.FromDays(days);
                }
                return TimeSpan.Zero;
            }

            var IsGuid = Guid.TryParse(args[1], out var UserGuid);

            if (!IsGuid)
            {
                Logger.Warning($"'{args[1]}' is an invalid Guid.", "server.mute");
                return;
            }

            var Client = server.Clients.Where(x => x.Key.Id == UserGuid).FirstOrDefault().Key;

            var TimeString = args[2];
            var MuteLength = ParseTimeString(TimeString);
            var MuteReason = string.Empty;
            args.Skip(3).ToList().ForEach(x => MuteReason += $"{x} ");

            if (MuteLength == TimeSpan.Zero)
            {
                Logger.Warning($"'{TimeString}' couldn't be parsed.");
                return;
            }

            // Firstly save 

            Client.Mute(MuteLength, MuteReason);
        }, "Mute a specific user, for a specific duration.");
        Add("server.unmute", (args, server) =>
        {
            if (args.Length != 2)
            {
                Logger.Warning($"usage: server.unmute <user-id>", "server.unmute");
                return;
            }

            var UserId = args[1];

            if (!Guid.TryParse(UserId, out var UserIdGuid))
            {
                Logger.Warning($"invalid user-id.", "server.unmute");
            }

            var clientsToUnmute = server.Clients.Where(x => x.Key.Id == UserIdGuid).ToList();

            clientsToUnmute.ForEach(x =>
            {
                // The date-time the mute will end is before the current time.
                x.Key.TimeMuted = DateTime.Now - TimeSpan.FromSeconds(1);
                x.Key.MuteReason = string.Empty;
                x.Key.Save();

                Execute("server.send", new string[4] { "server.send", $"{x.Key.Id}", "client.settitle", "DClient" }, Program.Server);
                Execute("server.pmsay", new string[3] { "server.pmsay", $"{x.Key.Id}", "You've been unmuted." }, Program.Server);
            });

        }, "Unmute a specific user.");
        Add("server.globalsay", (args, server) =>
        {
            string message = string.Empty;

            args.Skip(1).ToList().ForEach(x => message += $"{x} ");

            if (message == String.Empty)
                message = "No message provided";

            foreach (var (client, socket) in server.Clients)
            {
                socket.Message($"\n[Server] {ServerStorage.Format(message)}");
            }
        }, $"Sends a message, abbreviated by '[{Program.ServerName}]' to all connected clients.");
        Add("server.dmsay", (args, server) =>
        {
            if (args.Length < 2)
            {
                Logger.Warning($"invalid arguments", "server.dmsay");
                return;
            }
            var UserId = args[1];

            if (!Guid.TryParse(UserId, out var Uid))
            {
                Logger.Warning($"cannot dm '{UserId}', it's not a valid Guid.", "server.dmsay");
                return;
            }

            string DataToSend = string.Join(" ", args.Skip(2));
            var User = server.Clients.Where(x => x.Key.Id == Uid).First().Value;

            User.Message($"\n[{Program.ServerName}][PM] {DataToSend}");

        }, "Send a private message to a specific user.");
        Add("server.kickall", (args, server) =>
        {
            Client[] clients = server.Clients.Keys.ToArray();

            for (int i = 0; i < clients.Length; i++)
            {
                Logger.Info($"force disconnected {clients[i].UserName}");
                server.Clients[clients[i]].Message($"Server is shutting down.");
                server.Clients[clients[i]].Dispose();
                server.Clients.Remove(clients[i]);
            }

            Program.Accepting = false;

        }, "Kick all clients. Acts as a panic button. This also disables new connections until you specifiy server.accepting true");
        Add("server.accepting", (args, server) =>
        {
            if (args.Length != 2)
            {
                Logger.Info($"server.accepting: {Program.Accepting}");
                return;
            }

            if (!bool.TryParse(args[1], out var value))
            {
                Logger.Warning($"'{args[1]}' is not a valid boolean value.");
                return;
            }
            Program.Accepting = value;
        }, "Set a [System.Boolean][True|False] on whether the server is accepting new connections.");
        Add("server.hostname", (args, server) =>
        {
            if (args.Length > 1)
            {
                Console.WriteLine($"To change your host-name, you need to change the current users name.");
                return;
            }

            Console.WriteLine(Dns.GetHostName());
        }, "Display the servers hostname.");
        Add("server.ban", (args, server) =>
        {
            if (args.Length <= 2)
            {
                Logger.Warning($"usage: server.ban <user-id> <reason>", "server.ban");
                return;
            }
            var GuidString = args[1];

            if (!Guid.TryParse(GuidString, out var value))
            {
                Logger.Warning($"failed to parse Guid.", "server.ban");
                return;
            }

            var Reason = string.Join(" ", args.Skip(2));

            var IsUser = server.Clients.UserExists(value);

            if (!IsUser)
            {
                Logger.Warning($"cannot ban '{value}', they aren't connected or no user is assigned to that Id.", "server,ban");
                return;
            }

            var IsConnected = server.Clients.Where(x => x.Key.Id == value).Count() > 0;

            if (IsConnected)
            {
                var (User, Socket) = server.Clients.Where(x => x.Key.Id == value).First();
                Program.CommandManager.Execute("server.dmsay", new string[3] { "server.dmsay", $"{User.Id}", "You've been banned." }, server);

                Socket.Dispose();
                server.Clients.Remove(User);
                User.Ban(Reason);
                return;
            }

            var DummyClient = Client.Create(value, "");

            DummyClient.Ban(Reason);

        }, "Bans a specific user.");
        Add("server.unban", (args, server) =>
        {
            if (args.Length > 2)
            {
                Logger.Warning($"usage: server.unban <user-id>", "server.unban");
                return;
            }
            var GuidString = args[1];

            if (!Guid.TryParse(GuidString, out var value))
            {
                Logger.Warning($"failed to parse Guid.", "server.ban");
                return;
            }

            if (!Directory.Exists($"{Directory.GetCurrentDirectory()}/users/{value}"))
            {
                Logger.Info($"no user has ever connected with that Id.");
                return;
            }

            Client? ReadClient = JsonConvert.DeserializeObject<Client>(File.ReadAllText($"{Directory.GetCurrentDirectory()}/users/{value}/USER.JSON"));

            ReadClient.IsBanned = false;
            ReadClient.BanReason = String.Empty;
            ReadClient?.Save();

            ReadClient?.Dispose();

            Logger.Info($"Unbanned {ReadClient.UserName}", "server.unban");

        }, $"Unban a specfic user, check {Directory.GetCurrentDirectory()}/users for all user Ids.");
        Add("server.capacity", (args, server) =>
        {
            if (args.Length == 1)
            {
                Logger.Info($"server.capacity: {Program.MaxCapacity}");
                return;
            }
            var NewCapacity = args[1];

            if (!int.TryParse(NewCapacity, out var value))
            {
                Logger.Warning($"Failed to parse {NewCapacity} as an integer.", "server.capacity");
                return;
            }
            RegistryController.Write("MaxCapacity", value);
        }, "get or set the maximum capacity of the server.");
        Add("server.store", (args, server) =>
        {
            // server.store -f|-s <key> <value>

            if (args.Length < 3)
            {
                Logger.Warning($"usage: server.store [-f|-s] <key> [value] [-f: fetch key value, -s: save key with value]");
                return;
            }

            var Option = args[1];

            if (Option == "-f")
            {
                var Key = args[2];

                if (!ServerStorage.Query(Key))
                {
                    Logger.Warning($"cannot fetch '{Key}', it has no value or doesn't exist.");
                    return;
                }

                Logger.Info($"{Key}: {ServerStorage.Fetch(Key)}");
                return;
            }
            else if (Option == "-s")
            {
                var Key = args[2];
                var Value = args[3];

                if (ServerStorage.Query(Key))
                    ServerStorage.Set(Key, Value);
                else
                    ServerStorage.Insert(Key, Value);

                return;
            }
            else
            {
                Logger.Warning($"unknown option [{Option}]. Available options are [-f, -s]");
                return;
            }
        }, "fetch or save a value saved on the server.");
        Add("server.grant", (args, server) =>
        {
            if (args.Length != 3)
            {
                Logger.Warning($"usage: server.grant <userid> <permission>");
                return;
            }

            if (!Guid.TryParse(args[1], out var id))
            {
                Logger.Warning($"'{args[1]}' is not a valid user-id");
                return;
            }

            if (!server.Clients.UserExists(id))
            {
                Logger.Warning($"No such user exists.");
                return;
            }

            var (Client, Socket) = server.Clients.ContainsClient(id);

            if (Client is null && Socket is null)
            {
                Logger.Warning($"can only give permissions to connected users.");
                return;
            }

            Client?.Permissions.Give(args[2]);
            Socket?.Message($"{Program.ServerName} granted you the permission '{args[2]}'");
            Logger.FLog($"console granted {Client!.UserName} the permission {args[2]}");

        }, "Grant a specific user a permission");
        Add("server.revoke", (args, server) =>
        {
            if (args.Length != 3)
            {
                Logger.Warning($"usage: server.grant <userid> <permission>");
                return;
            }

            if (!Guid.TryParse(args[1], out var id))
            {
                Logger.Warning($"'{args[1]}' is not a valid user-id");
                return;
            }

            if (!server.Clients.UserExists(id))
            {
                Logger.Warning($"No such user exists.");
                return;
            }

            var (Client, Socket) = server.Clients.ContainsClient(id);

            if (Client is null && Socket is null)
            {
                Logger.Warning($"can only give permissions to connected users.");
                return;
            }

            Client?.Permissions.Revoke(args[2]);
            Socket?.Message($"{Program.ServerName} revoked you the permission '{args[2]}'");
            Logger.FLog($"console revoked {Client!.UserName} the permission {args[2]}");

        }, "Revoke a permission from a user");

        // internal server based information, like owner-name, server-email, etc...

        Add("info.owner", (args, server) =>
        {
            if (args.Length < 2)
            {
                if (ServerStorage.Query("info.owner"))
                {
                    Logger.Info($"info.owner: {ServerStorage.Fetch("info.owner")}");
                }

                return;
            }

            var Result = string.Join(' ', args.Skip(1));

            ServerStorage.Set("info.owner", Result);
            Owner = Result;

            ServerStorage.Save(ServerStoragePath);
        }, "Set the server owner string.");
        Add("info.email", (args, server) =>
        {
            if (args.Length != 2)
            {
                Logger.Info($"info.email: {EMail}");
                return;
            }

            ServerStorage.Set("info.email", args[1]);
            EMail = args[1];

            ServerStorage.Save(ServerStoragePath);
        }, "Set the server e-mail");
        Add("info.copyright", (args, server) =>
        {
            Console.WriteLine(Program.Copyright);
        }, $"display the copyright information about this server build. ({Program.Version})");
        Add("info.motd", (args, server) =>
        {
            if (args.Length == 1)
            {
                Console.WriteLine($"info.motd: {ServerStorage.Fetch("motd")}");
                return;
            }
            var message = string.Join(' ', args);
            if (ServerStorage.Query("motd"))
            {
                ServerStorage.Set("motd", message);
                return;
            }
            else
            {
                ServerStorage.Set("motd", message);
            }

        }, "This message will be displayed when the client connects to the server. Short for 'Message Of The Day'");

        Logger.Info($"Initialized {Commands.Count} internal commands", "ServerCommandManager");
    }

    public void Add(string name, Action<string[], ServerController> action, string whatDoesItDo)
    {
        if (!name.Contains('.'))
        {
            Console.WriteLine($"command name syntax should be 'module.command'. (for \"{name}\") adding anyway");
        }

        Commands.Add(name, Command.Create(action, whatDoesItDo));
    }
    public bool Fetch(string name, out Action<string[], ServerController>? function)
    {
        if (!Commands.TryGetValue(name, out var command))
        {
            Console.WriteLine($"could not fetch command {name}.");
            function = null;
            return false;
        }

        function = command.Function;
        return true;
    }

    public void Execute(string name, string[] args, ServerController server)
    {
        if (!Commands.ContainsKey(name))
        {
            Logger.Warning($"attempted to execute command that does not exist. ({name})", "Execute");
            return;
        }

        if (args.Contains("--help"))
        {
            Console.WriteLine($"{name}: {Commands[name].Description}");
            return;
        }

        Commands[name].Function(args, server);
    }

    public static (string, string[]) DisectArgs(string args)
    {
        if (args.Length == 0)
            throw new ArgumentException("cannot disect an empty string.");

        if (args.Length == 1)
            return (args.Split(' ')[0], Array.Empty<string>());

        var commandName = args.Split(' ')[0];
        var arguments = args.Split(' ').Skip(0).ToArray();

        return (commandName, arguments);
    }

    public IDictionary<string, Command> Commands { get; }
}
