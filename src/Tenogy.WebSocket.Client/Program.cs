using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace Tenogy.WebSocket.Client
{
	class Program
	{
		private static HubConnection _hubConnection;
		private static bool exit;

		static async Task Main(string[] args)
		{
			_hubConnection = new HubConnectionBuilder()
				.WithUrl("https://localhost:44353/terminal")
				.Build();

			_hubConnection.On<string>("Output", output =>
			{
				Console.WriteLine(output);
			});

			_hubConnection.On("Exit", () => { exit = true; });

			await _hubConnection.StartAsync();

			while (!exit)
			{
				string cmd = ReadLine();
				await Command(cmd);
			}

		}

		public static string ReadLine()
		{
			//Console.Write("# "); 
			return Console.ReadLine();
		}

		static Task Command(string command) =>
			_hubConnection.SendAsync("Command", command);
	}
}
