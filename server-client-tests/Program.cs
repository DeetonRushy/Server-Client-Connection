using System.Diagnostics;

const string ServerFilePath =
    "C:\\Users\\ddt\\source\\repos\\client-server\\server\\bin\\Debug\\net6.0\\server.exe";

const string ClientFilePath =
    "C:\\Users\\ddt\\source\\repos\\client-server\\client\\bin\\Debug\\net6.0\\client.exe";


Process server = new Process();

server.StartInfo.FileName = ServerFilePath;
server.StartInfo.UseShellExecute = true;
Process client = new Process();
client.StartInfo.FileName = ClientFilePath;
client.StartInfo.UseShellExecute = true;

server.Start();
client.Start();

server.WaitForExit();
client.Kill();