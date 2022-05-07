## C# Client-Server Connection (Windows)

This is my first ever experience with anything connection based. It's a simple client-server connection. The client can be controlled by the server with commands sent over sockets (in plaintext). It requires the .NET6.0 Runtime. 

### Disclaimer
This is very far from **secure** and should not, in any circumstance be used for any actual connections between real clients in it's current state.

## Documentation (Attempt)

### ServerController
This is the object that handles all connection based operations. This includes client connections, OnMessageReceived handler, all client connection data. It also contains server-sided title change management *system* which is purely visual. 

**BeginListen** will start the server.

### ServerBuilder
Used to build a **ServerController** while keeping syntax **relatively** bareable to look at.

### RegistryController
This is a *static* class used to Read/Write into the servers registry key. 

You **must** call *RegistryController.Initialize()* to ensure default keys are in place. To add default keys, follow below

```cs
RegistryController.DefaultRegValues.Add(
   "ServerValue",
   new object()
)
```
do this before calling initialize.

### ServerCommandManager
This object handles all user input & contains server commands. The defaults are as follows:

| Name | Action |
|--|--|
| visual.name | get or set the server name. this same is shown in messages from the server |
| visual.font | set the servers console font |
| server.help | display help for all commands, or supply a command name for a specific command |
| server.port | set the servers port (doesn't really do anything)
| server.send | Send an action to be executed on the client. usage: server.send [user-id] [action] 
| server.clients | lists all connected clients information. |
| server.mute | mute a specific user, for a specific duration |
| server.unmute | unmute a specific user |
| server.globalsay | sends a message to all connected clients, abbreviated by **visual.name** |
| server.dmsay | sends a message to a specific user. |
| server.accepting | set a Boolean that determines whether the server is accepting new connections. |
| server.hostname | get the name of the current machine |
| server.ban | ban a specific user |
| server.unban | unban a specific user |
| server.capacity | get or set the maximum amount of clients that can be connected at once. |
| info.owner | get or set the server-owner string |
| info.email | get or set the server-email string |
| info.copyright | get the MIT license for this application |

These commands are in a dictionary of <string, Action<string[], ServerController>>. 

| Function | What It Does |
|--|--|
| Server.Add(string, Action<string[], ServerController>, string): *add a command* | Adds a command. |
| Fetch(string, out Action<string[], ServerController>): | retrieve a command callback. |
| Execute(string, string[], ServerController) | execute a command. If it doesn't exist, there will be an error message logged to the console.
| DisectArgs(string args) | takes the string the server operator has typed, sorts it into a string[] suitable for Execute.

### Client
This object encapsulates all information about a connected client. It contains their unique identifier, their name, whether they're banned or muted, their mute duration (if applicable), their ban reason (if applicable), their mute reason (if applicable) and how long they've been connected.

The user is also serializable, every user that will ever connect has their own filesystem directory under \\users. The directory is named their Id. It contains a file called **USER.json**, this contains all user data and is saved regularly. 

When a client object is instantiated, if the user already exists it will load their previous information, such as the things that shouldn't be client sided like ban status & mute status. If they haven't connected before, a new folder and file will be made.

### Logger
This is a basic console logger that will display who logged the information (if supplied), the type of information it is (Error, Info, Warning, Fatal) and the exact time it will logged.

### Client (project)
It's very simple and I really can't be bothered documenting it. 

## Have fun!
This is by no means serious, so if you're looking for a solution for a project you're putting a lot into, I wouldn't bother with this. However, if you're looking to have fun and explore the possibilities of C# sockets & client-server connections in C#, go ahead :)
