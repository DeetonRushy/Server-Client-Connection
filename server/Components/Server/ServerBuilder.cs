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
        _controller.OnMessageReceived = callback;
        return this;
    }
    public ServerBuilder WithPort(int port)
    {
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
        _controller.AddTitleInfo(Evaluator);
        return this;
    }
    public ServerBuilder WithOnConnectionAccepted( Func<Socket, bool> callback )
    {
        _controller.OnClientConnection = callback;
        return this;
    }
    public ServerController Build()
    {
        return _controller;
    }
    private ServerController _controller;
    public static ServerBuilder Create()
        => new();
}
