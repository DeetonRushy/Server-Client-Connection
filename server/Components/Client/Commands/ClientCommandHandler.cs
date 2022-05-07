using System.Net.Sockets;

namespace Server;

public class ClientCommand
{
    public ClientCommand
        (
        string Name,
        Action<Client, Socket, string[]> Callback,
        string[] RequiredPermissions
        )
    {
        this.Name = Name;
        this.Callback = Callback;
        this.RequiredPermissions = RequiredPermissions;
    }

    public void Execute(Client client, Socket socket, string[] args)
    {
        foreach (var perm in RequiredPermissions)
        {
            if (!client.Permissions.HasPermission(perm))
            {
                socket.Message($"insufficient permissions to execute {Name}");
                return;
            }
        }

        Callback(client, socket, args);
    }

    public string Name { get; set; } = string.Empty;
    public Action<Client, Socket, string[]> Callback { get; set; }
    public string[] RequiredPermissions { get; set; }

    public static ClientCommand Create(string Name, Action<Client, Socket, string[]> Callback, params string[] Permissions)
        => new(Name, Callback, Permissions);
}

public class ClientCommandHandler
{
    public IList<ClientCommand> Commands { get; } = new List<ClientCommand>();

    public ClientCommandHandler()
    {
        Logger.Info($"Initialized", "ClientCommandHandler");

        var SayCommand
            = ClientCommand.Create
            ("say", (client, socket, args) =>
            {
                var Original = string.Empty;
                args.ToList().ForEach(x => Original += x);

                if (client.IsMuted)
                {
                    socket.Message($"cannot use 'say' while muted.");
                    return;
                }

                Logger.Info($"{client.UserName}> {Original}");

                foreach (var (user, sock) in Program.Server.Clients)
                {
                    if (user.Id == client.Id)
                        continue; // dont send the sender their own message
                    sock.Message($"{client.UserName}> {Original}");
                }
            }, "say");

        Commands.Add(SayCommand);

        var BanCommand
            = ClientCommand.Create
            ("ban", (client, socket, args) =>
            {
                if (args.Length < 1)
                {
                    socket.Message($"usage: :ban <user-id> [reason]");
                    return;
                }

                var UserId = args[0];

                if (!Guid.TryParse(UserId, out var Id))
                {
                    socket.Message($"unknown user '{UserId}'");
                    return;
                }
                if (Id == client.Id)
                {
                    socket.Message($"you cannot ban yourself.");
                    return;
                }
                if (!Program.Server.Clients.UserExists(Id))
                {
                    socket.Message($"no user with that ID exists.");
                    return;
                }

                var Reason = string.Join(' ', args.Skip(1));

                if (Reason.Length == 0)
                    Reason = "No reason supplied.";

                Client? User = null;

                if (Program.Server.Clients.UserConnected(Id))
                {
                    var UserKvp = Program.Server.Clients.Where(x => x.Key.Id == Id).First();
                    User = UserKvp.Key;
                    User.Ban(Reason);
                    UserKvp.Value.Disconnect(true);
                    return;
                }
                else
                {
                    User = new Client(Id, string.Empty);
                    User.Ban(Reason);
                }

                socket.Message($"banned {User.UserName} permanently");

            }, "ban");

        Commands.Add(BanCommand);
    }

    public void Add
        (
        string Name,
        Action<Client, Socket, string[]> Handler,
        params string[] perms
        )
    {
        if (perms.Length == 0)
        {
            Logger.Warning($"create a client command with no required permissions means any user can run that command.", "ClientCommandHandler.Add");
        }

        var Command = ClientCommand.Create(Name, Handler, perms);

        Commands.Add(Command);
    }

    public void Remove
        (
        string Name
        )
    {
        var SelectedCommand = Commands.FirstOrDefault(x => x.Name == Name);

        if (SelectedCommand!.Name == string.Empty)
            return;

        Commands.Remove(SelectedCommand);
    }

    public bool Handle
        (
        string MessageFromClient, 
        ServerController server
        )
    {
        // We expect messages to be layed out as follow 'Guid:Data:Data:...' from the client.
        var ClientData = MessageFromClient.Split(':');

        if (ClientData.Length == 0)
            return false;

        var ExpectedGuid = ClientData[0];

        if (!Guid.TryParse(ExpectedGuid, out Guid UserId))
        {
            return false;
        }

        // We have their Guid, lets fetch the client before anything.

        var (Client, Socket) = server.Clients.ContainsClient(UserId);

        if (Client == null || Socket == null)
        {
            Logger.Warning($"received message from {UserId} but they're not connected. (?)");
            return false;
        }

        // Okay, they're connected. Lets see if this message contains a command.

        if (ClientData.Length == 1)
            return false;

        var CommandString = ClientData[1];
        var MaybeCommand = Commands.Where(x => x.Name == CommandString);

        if (!MaybeCommand.Any())
        {
            // It's not a command.
            Socket.Message($"{CommandString} is not a recognized command.");
            return false;
        }

        var Command = MaybeCommand.First();
        var Arguments = ClientData[2].Split();

        // The command itself will check if it can be executed by Client. So, lets
        // get the arguments of the command.

        string[] Args = new string[Arguments.Length];
        for (int i = 0; i < Arguments.Length; i++)
        {
            Args[i] = Arguments[i] + " ";
        }

        // Execute the command.

        Command.Execute(Client, Socket, Args);

        return true;
    }
}
