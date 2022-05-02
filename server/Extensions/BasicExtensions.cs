using System.Net.Sockets;

namespace Server;

public static class BasicExtensions
{
    public static (Client?, Socket?) ContainsClient(this IDictionary<Client, Socket> dict, Guid guid)
    {
        foreach (var (client, socket) in dict)
        {
            if (client.Id == guid)
            {
                return (client, socket);
            }
        }

        return (null, null);
    }
    public static string[] Splice(this byte[] array, char delim)
    {
        var result = ServerController.GetString(array);
        return result.Split(delim);
    }
    public static string RemoveFirst(this string[] array)
    {
        var newArray = new List<string>();

        for (int i = 1; i < array.Length; i++)
        {
            newArray.Add(array[i]);
        }

        string result
            = string.Join(':', newArray);

        return result;
    }
    public static void Message(this Socket socket, string message)
    {
        socket.Send(ServerController.GetBytes(message));
    }
    public static Socket? Find(this IDictionary<Client, Socket> dict, string guidOrUserName)
    {
        if (!Guid.TryParse(guidOrUserName, out var guid))
        {
            // assume it's a name
            var expectedPair = dict.Where(x => x.Key.UserName == guidOrUserName);
            if (expectedPair.Count() == 0)
            {
                Console.WriteLine($"cannot pm {guidOrUserName} because they aren't connected.");
                return null;
            }
            return expectedPair.First().Value;
        }

        var expectedPairGuid = dict.Where(x => x.Key.Id == guid);
        if (expectedPairGuid.Count() == 0)
        {
            Console.WriteLine($"cannot pm {guid}.");
            return null;
        }
        return expectedPairGuid.First().Value;

    }
    public static bool UserExists(this IDictionary<Client, Socket> dict, Guid Id)
    {
        if (!Directory.Exists($"{Directory.GetCurrentDirectory()}/users/{Id}"))
            return false;
        return true;
    }
    public static bool UserConnected(this IDictionary<Client, Socket> dict, Guid Id)
    {
        return dict.Any(x => x.Key.Id == Id);
    }
    public static byte[] Bytes(this string str)
    {
        return System.Text.Encoding.UTF8.GetBytes(str);
    }
    public static string String(this byte[] arr)
    {
        return System.Text.Encoding.UTF8.GetString(arr);
    }
}


