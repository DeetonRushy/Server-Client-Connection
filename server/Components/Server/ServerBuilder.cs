using System.Net.Sockets;

namespace Server;

public class ServerBuilder
{
    public ServerBuilder()
    {
        _controller = new();
    }
    public ServerBuilder WithOnMessageReceived(Action<Client, Socket, string[]> callback)
    {
        Logger.FLog($"setting 'OnMessageReceived' callback to '{callback.Method.Name}'");
        _controller.OnMessageReceived = callback;
        return this;
    }
    public ServerBuilder WithPort(int port)
    {
        Logger.FLog($"starting server on port {port}");
        _controller.SetPort(port);
        return this;
    }
    public ServerBuilder WithTitleCallback(ParameterizedThreadStart callback)
    {
        _controller.OnTitleRefresh = callback;
        return this;
    }
    public ServerBuilder WithTitleInfo(Func<string> Evaluator)
    {
        Logger.FLog($"setting 'OnTitleInfo' callback to '{Evaluator.Method.Name}'");
        _controller.AddTitleInfo(Evaluator);
        return this;
    }
    public ServerBuilder WithOnConnectionAccepted( Func<Socket, bool> callback )
    {
        Logger.FLog($"setting 'OnConnectionAccepted' callback to '{callback.Method.Name}'");
        _controller.OnClientConnection = callback;
        return this;
    }
    public ServerController Build()
    {
        Logger.FLog($"built server controller");
        return _controller;
    }
    private ServerController _controller;
    public static ServerBuilder Create()
        => new();
}
