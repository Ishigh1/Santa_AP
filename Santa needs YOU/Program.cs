using Archipelago.Gifting.Net.Service;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;using Santa_needs_YOU;

Console.Write("Enter the server IP address: ");
string serverIp = Console.ReadLine() ?? throw new InvalidOperationException();
if (serverIp == "")
	serverIp = "localhost";

Console.Write("Enter the server port: ");
int serverPort;
if (!int.TryParse(Console.ReadLine(), out serverPort))
{
	serverPort = 38281;
}

// Prompt the user for username and password
Console.Write("Enter your username: ");
string username = Console.ReadLine() ?? throw new InvalidOperationException();
if (username == "")
	username = "Player1";

ArchipelagoSession session = ArchipelagoSessionFactory.CreateSession(serverIp, serverPort);

session.TryConnectAndLogin("Santa", username, ItemsHandlingFlags.AllItems);

new SantaHandler(session);


loop:

string? input = Console.ReadLine(); //Stops at ctrl+z
if (input == null) goto end;

string[] strings = input.Split(' ');
switch (strings[0])
{
	case "exit":
		goto end;
}

goto loop;

end: ;