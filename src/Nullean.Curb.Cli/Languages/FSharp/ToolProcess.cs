using System.Diagnostics;
using System.Text;

namespace Nullean.Curb.Cli.Languages.FSharp;

/// <summary>A process to start: the program, its arguments, and where it starts from.</summary>
internal sealed record ToolCommand(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory)
{
	public override string ToString() => $"{FileName} {string.Join(' ', Arguments)}";
}

/// <summary>Starts the processes Curb hands work to.</summary>
/// <remarks>
/// An interface so tests can stand a fake Fantomas behind it and count what was started. Not routed
/// through <c>IFileSystem</c>, because starting a process is not filesystem access; everything Curb
/// reads to decide what to start is.
/// </remarks>
internal interface IToolLauncher
{
	/// <summary>Starts <paramref name="command"/> with its standard streams redirected.</summary>
	/// <exception cref="ToolStartException">The process could not be started at all.</exception>
	IToolProcess Start(ToolCommand command);
}

/// <summary>A started process: its standard input and output as streams, and its error output as text.</summary>
internal interface IToolProcess : IDisposable
{
	Stream StandardInput { get; }

	Stream StandardOutput { get; }

	/// <summary>What the process wrote to standard error so far, kept for the message when something goes wrong.</summary>
	string StandardError { get; }

	/// <summary>Completes with the exit code.</summary>
	Task<int> Exited { get; }

	void Kill();
}

/// <summary>A process that could not be started.</summary>
internal sealed class ToolStartException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Starts real processes.</summary>
internal sealed class ProcessLauncher : IToolLauncher
{
	public static ProcessLauncher Instance { get; } = new();

	private ProcessLauncher() { }

	public IToolProcess Start(ToolCommand command)
	{
		var info = new ProcessStartInfo(command.FileName)
		{
			UseShellExecute = false,
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			WorkingDirectory = command.WorkingDirectory,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8,
		};
		foreach (var argument in command.Arguments)
			info.ArgumentList.Add(argument);

		// Not a terminal, so nothing it prints should try to colour itself for one.
		info.Environment["NO_COLOR"] = "1";
		info.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
		info.Environment["DOTNET_NOLOGO"] = "1";

		try
		{
			var process = Process.Start(info) ?? throw new ToolStartException($"`{command}` did not start");
			return new RealProcess(process);
		}
		catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
		{
			throw new ToolStartException($"`{command}` could not be started — {exception.Message}", exception);
		}
	}

	private sealed class RealProcess : IToolProcess
	{
		/// <summary>Enough of standard error to explain a failure, and no more: a chatty tool must not grow Curb's memory.</summary>
		private const int ErrorLimit = 16 * 1024;

		private readonly Process _process;
		private readonly StringBuilder _error = new();
		private readonly Task _errorPump;

		public RealProcess(Process process)
		{
			_process = process;

			// Drained continuously, so a tool that writes a lot to standard error never blocks on a full pipe
			// while Curb waits on its standard output.
			_errorPump = Task.Run(async () =>
			{
				var buffer = new char[4096];
				int read;
				while ((read = await process.StandardError.ReadAsync(buffer)) > 0)
				{
					lock (_error)
					{
						if (_error.Length < ErrorLimit)
							_error.Append(buffer, 0, Math.Min(read, ErrorLimit - _error.Length));
					}
				}
			});

			Exited = WaitAsync();
		}

		public Stream StandardInput => _process.StandardInput.BaseStream;

		public Stream StandardOutput => _process.StandardOutput.BaseStream;

		public string StandardError
		{
			get
			{
				lock (_error)
					return _error.ToString();
			}
		}

		public Task<int> Exited { get; }

		private async Task<int> WaitAsync()
		{
			await _process.WaitForExitAsync();
			try
			{
				await _errorPump.WaitAsync(TimeSpan.FromSeconds(5));
			}
			catch (TimeoutException)
			{
				// A grandchild holding the pipe open; what was read is enough.
			}

			return _process.ExitCode;
		}

		public void Kill()
		{
			try
			{
				_process.Kill(entireProcessTree: true);
			}
			catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
			{
				// Already gone.
			}
		}

		public void Dispose() => _process.Dispose();
	}
}
