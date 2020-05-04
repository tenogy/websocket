using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Tenogy.WebSocket.Server
{
	public class TerminalHub : Hub
	{
		private readonly IHubContext<TerminalHub> _hubContext;
		private static readonly ConcurrentDictionary<string, Process> _processes = new ConcurrentDictionary<string, Process>();

		public TerminalHub(IHubContext<TerminalHub> hubContext)
		{
			_hubContext = hubContext;
		}

		public override async Task OnConnectedAsync()
		{
			await base.OnConnectedAsync();

			var process = new Process
			{
				EnableRaisingEvents = true,
				StartInfo = new ProcessStartInfo
				{
					CreateNoWindow = true,
					ErrorDialog = false,
					FileName = "cmd.exe",
					RedirectStandardError = true,
					RedirectStandardInput = true,
					RedirectStandardOutput = true,
					UseShellExecute = false,
					WorkingDirectory = Environment.CurrentDirectory,
				},
			};

			process.OutputDataReceived += async (s, e) => await SendDataAsync(s as Process, e.Data).ConfigureAwait(false);
			process.ErrorDataReceived += async (s, e) => await SendDataAsync(s as Process, e.Data).ConfigureAwait(false);
			process.Exited += async (s, e) =>
			{
				var connectionId = FindConnection(process);
				if (connectionId == null) return;
				await _hubContext.Clients.Clients(connectionId).SendAsync("Exit");
			};

			_processes.TryAdd(Context.ConnectionId, process);

			process.Start();
			process.BeginErrorReadLine();
			process.BeginOutputReadLine();
		}

		public override Task OnDisconnectedAsync(Exception exception)
		{
			if (_processes.TryRemove(Context.ConnectionId, out var process))
			{
				if (!process.HasExited)
					process.Kill();

				process.Dispose();
			}

			return base.OnDisconnectedAsync(exception);
		}

		public Task Command(string command)
		{
			return _processes.TryGetValue(Context.ConnectionId, out var process)
				? process.StandardInput.WriteLineAsync(command)
				: Task.CompletedTask;
		}


		private Task SendDataAsync(Process process, string data)
		{
			if (process.HasExited)
				return Task.CompletedTask;

			var connectionId = FindConnection(process);
			return _hubContext.Clients.Client(connectionId).SendAsync("Output", data);
		}

		private string FindConnection(Process p)
			=> _processes.FirstOrDefault(kvp => kvp.Value == p).Key;
	}
}
