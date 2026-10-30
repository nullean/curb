using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Abstractions.TestingHelpers;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Nullean.Curb.Cli.Languages.FSharp;

namespace Nullean.Curb.Tests.Cli.FSharp;

/// <summary>
/// Stands in for Fantomas behind <see cref="IToolLauncher"/>: a daemon speaking the real wire format over
/// in-memory pipes, and a batch CLI that reads and writes the <see cref="MockFileSystem"/>. Counts what was
/// started, so a test can prove a run started nothing.
/// </summary>
/// <remarks>
/// Its "formatting" is a toy whose answers are steered by markers in the source:
/// <list type="bullet">
/// <item>runs of spaces collapse to one, so <c>let  x</c> needs formatting and <c>let x</c> does not;</item>
/// <item><c>BROKEN</c> does not parse;</item>
/// <item><c>INVALID</c> formats to something containing <c>BROKEN</c>, so the re-check finds output that does not parse;</item>
/// <item><c>UNSTABLE</c> grows a character on every pass, so the re-check finds another change;</item>
/// <item>a name listed in the nearest <c>.fantomasignore</c> is ignored.</item>
/// </list>
/// </remarks>
internal sealed class FakeFantomas(MockFileSystem fileSystem) : IToolLauncher
{
	public string Version { get; set; } = "8.1.0+0123456789abcdef";

	/// <summary>The daemon cannot be started at all.</summary>
	public bool DaemonFailsToStart { get; set; }

	/// <summary>The daemon starts, then exits without answering its handshake.</summary>
	public bool DaemonExitsBeforeHandshake { get; set; }

	/// <summary>The daemon answers its handshake and then never answers a document.</summary>
	public bool DaemonStalls { get; set; }

	/// <summary>The daemon answers each document as though it had been asked about this path instead.</summary>
	public string? DaemonAnswersFor { get; set; }

	/// <summary>The command line never finishes.</summary>
	public bool CliStalls { get; set; }

	/// <summary>The command line does its work, then prints something that is not its report.</summary>
	public bool CliPrintsGarbage { get; set; }

	/// <summary>The command line leaves each file it rewrote read-only, so it cannot be put back.</summary>
	public bool CliLeavesReadOnly { get; set; }

	/// <summary>How many processes were killed.</summary>
	public int Kills => _kills;

	private int _kills;

	internal void Killed() => Interlocked.Increment(ref _kills);

	/// <summary>Settings to report as unknown in a configuration warning, after every request.</summary>
	public string[] UnknownSettings { get; set; } = [];

	public ConcurrentQueue<ToolCommand> Started { get; } = new();

	/// <summary>Every document the daemon was asked to format, in arrival order.</summary>
	public ConcurrentQueue<(string Path, string Source)> Requests { get; } = new();

	public int DaemonsStarted => Started.Count(IsDaemon);

	public int CliRuns => Started.Count(command => !IsDaemon(command));

	private static bool IsDaemon(ToolCommand command) => command.Arguments.Contains("daemon") || command.Arguments.Contains("--daemon");

	public IToolProcess Start(ToolCommand command)
	{
		Started.Enqueue(command);
		if (IsDaemon(command))
		{
			if (DaemonFailsToStart)
				throw new ToolStartException("no such file");
			return new Daemon(this);
		}

		if (CliStalls)
		{
			// Formats in place first, as a real run would have started to, then never reports.
			if (!command.Arguments.Contains("check"))
				RunCli(command);
			return new FakeProcess(this);
		}

		return RunCli(command);
	}

	/// <summary>The toy formatter. Null for a file that does not parse.</summary>
	internal static string? Format(string source)
	{
		if (source.Contains("BROKEN", StringComparison.Ordinal))
			return null;
		if (source.Contains("INVALID", StringComparison.Ordinal))
			return source.Replace("INVALID", "BROKEN", StringComparison.Ordinal);
		if (source.Contains("UNSTABLE", StringComparison.Ordinal))
			return source + "!";

		var text = source;
		while (text.Contains("  ", StringComparison.Ordinal))
			text = text.Replace("  ", " ", StringComparison.Ordinal);
		return text;
	}

	private bool Ignored(string path)
	{
		for (var directory = Path.GetDirectoryName(path); !string.IsNullOrEmpty(directory); directory = Path.GetDirectoryName(directory))
		{
			var ignore = Path.Combine(directory, ".fantomasignore");
			if (fileSystem.File.Exists(ignore))
				return fileSystem.File.ReadAllLines(ignore).Any(line => line.Trim() == Path.GetFileName(path));
		}

		return false;
	}

	private FakeProcess RunCli(ToolCommand command)
	{
		var check = command.Arguments.Contains("check");
		var paths = command.Arguments.Where(argument => argument.StartsWith('/') && !argument.EndsWith(".dll", StringComparison.Ordinal));

		var output = new MemoryStream();
		using (var json = new Utf8JsonWriter(output))
		{
			json.WriteStartObject();
			json.WriteString("command", check ? "check" : "format");
			json.WriteNull("error");
			json.WriteStartArray("files");
			foreach (var path in paths)
			{
				if (Ignored(path))
					continue;

				var source = fileSystem.File.ReadAllText(path);
				var formatted = Format(source);
				json.WriteStartObject();
				json.WriteString("path", path);
				if (formatted is null)
				{
					json.WriteString("status", "error");
					json.WriteString("message", $"{path} could not be parsed by Fantomas");
				}
				else if (formatted.Contains("BROKEN", StringComparison.Ordinal))
				{
					// The CLI validates what it produced, unlike the daemon.
					json.WriteString("status", "error");
					json.WriteString("message", $"{path}: Fantomas produced invalid code");
				}
				else if (formatted == source)
					json.WriteString("status", "unchanged");
				else
				{
					json.WriteString("status", check ? "needs-formatting" : "formatted");
					if (!check)
					{
						fileSystem.File.WriteAllText(path, formatted);
						if (CliLeavesReadOnly)
							fileSystem.File.SetAttributes(path, FileAttributes.ReadOnly);
					}
				}
				json.WriteEndObject();
			}
			json.WriteEndArray();
			json.WriteEndObject();
		}

		var process = new FakeProcess(this);
		process.ToClient.WriteAndComplete(CliPrintsGarbage ? "Unhandled exception. not a report"u8.ToArray() : output.ToArray());
		process.Exit(0);
		return process;
	}

	/// <summary>A daemon: reads framed requests, answers each from a task of its own so answers can overtake one another.</summary>
	private sealed class Daemon : FakeProcess
	{
		private readonly FakeFantomas _owner;
		private readonly SemaphoreSlim _writeLock = new(1, 1);

		public Daemon(FakeFantomas owner) : base(owner)
		{
			_owner = owner;
			_ = Task.Run(ServeAsync);
		}

		private async Task ServeAsync()
		{
			var pending = new List<Task>();
			while (await ReadFrameAsync() is { } body)
			{
				var request = JsonDocument.Parse(body).RootElement;
				var id = request.GetProperty("id").GetInt64();
				var method = request.GetProperty("method").GetString();
				if (method == "fantomas/version")
				{
					if (_owner.DaemonExitsBeforeHandshake)
						break;

					await SendAsync(writer => writer.WriteString("result", _owner.Version), id);
					continue;
				}

				var parameters = request.GetProperty("params");
				var path = parameters.GetProperty("FilePath").GetString()!;
				var source = parameters.GetProperty("SourceCode").GetString()!;
				_owner.Requests.Enqueue((path, source));
				pending.Add(Task.Run(async () =>
				{
					if (_owner.DaemonStalls)
					{
						await Task.Delay(Timeout.Infinite, KilledToken).ContinueWith(_ => { }, TaskScheduler.Default);
						return;
					}

					await Task.Delay(Random.Shared.Next(0, 3));
					await SendWarningAsync(path);
					await SendAsync(writer => WriteResult(writer, _owner.DaemonAnswersFor ?? path, source), id);
				}));
			}

			await Task.WhenAll(pending);
			ToClient.Complete();
			Exit(0);
		}

		private void WriteResult(Utf8JsonWriter writer, string path, string source)
		{
			writer.WriteStartObject("result");
			if (_owner.Ignored(path))
			{
				writer.WriteString("Case", "IgnoredFile");
				writer.WriteStartArray("Fields");
				writer.WriteStringValue(path);
				writer.WriteEndArray();
			}
			else if (Format(source) is not { } formatted)
			{
				writer.WriteString("Case", "Error");
				writer.WriteStartArray("Fields");
				writer.WriteStringValue(path);
				writer.WriteStringValue($"{path} could not be parsed by Fantomas:\n\n{path}(1,1): error FS0010: Unexpected BROKEN\n");
				writer.WriteEndArray();
			}
			else if (formatted == source)
			{
				writer.WriteString("Case", "Unchanged");
				writer.WriteStartArray("Fields");
				writer.WriteStringValue(path);
				writer.WriteEndArray();
			}
			else
			{
				writer.WriteString("Case", "Formatted");
				writer.WriteStartArray("Fields");
				writer.WriteStringValue(path);
				writer.WriteStringValue(formatted);
				writer.WriteNullValue();
				writer.WriteEndArray();
			}
			writer.WriteEndObject();
		}

		private Task SendWarningAsync(string path) =>
			SendAsync(writer =>
			{
				writer.WriteString("method", "fantomas/configurationWarning");
				writer.WriteStartArray("params");
				writer.WriteStartObject();
				writer.WriteString("FilePath", path);
				writer.WriteStartArray("Problems");
				foreach (var setting in _owner.UnknownSettings)
				{
					writer.WriteStartObject();
					writer.WriteNumber("Code", 1);
					writer.WriteString("Setting", setting);
					writer.WriteNull("Value");
					writer.WriteEndObject();
				}
				writer.WriteEndArray();
				writer.WriteEndObject();
				writer.WriteEndArray();
			}, id: null);

		private async Task SendAsync(Action<Utf8JsonWriter> write, long? id)
		{
			var body = new MemoryStream();
			using (var writer = new Utf8JsonWriter(body))
			{
				writer.WriteStartObject();
				writer.WriteString("jsonrpc", "2.0");
				if (id is { } value)
					writer.WriteNumber("id", value);
				write(writer);
				writer.WriteEndObject();
			}

			var header = Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"Content-Length: {body.Length}\r\n\r\n"));
			await _writeLock.WaitAsync();
			try
			{
				// Header and body in separate writes, so the client has to put a message together from pieces.
				ToClient.Push(header);
				ToClient.Push(body.ToArray());
			}
			finally
			{
				_writeLock.Release();
			}
		}

		private async Task<byte[]?> ReadFrameAsync()
		{
			var header = new List<byte>();
			var one = new byte[1];
			while (!(header.Count >= 4 && header[^4] == '\r' && header[^3] == '\n' && header[^2] == '\r' && header[^1] == '\n'))
			{
				if (await FromClient.ReadAsync(one) == 0)
					return null;
				header.Add(one[0]);
			}

			var text = Encoding.ASCII.GetString([.. header]);
			var length = int.Parse(text.Split(':')[1].Trim(), CultureInfo.InvariantCulture);
			var body = new byte[length];
			var read = 0;
			while (read < length)
			{
				var n = await FromClient.ReadAsync(body.AsMemory(read));
				if (n == 0)
					return null;
				read += n;
			}

			return body;
		}
	}
}

/// <summary>A process made of two in-memory pipes.</summary>
internal class FakeProcess(FakeFantomas owner) : IToolProcess
{
	private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private readonly CancellationTokenSource _killed = new();

	/// <summary>Cancelled when the process is killed, so a stalled fake can stop.</summary>
	protected CancellationToken KilledToken => _killed.Token;

	public ChannelStream FromClient { get; } = new();

	public ChannelStream ToClient { get; } = new();

	public Stream StandardInput => FromClient;

	public Stream StandardOutput => ToClient;

	public string StandardError => "";

	public Task<int> Exited => _exited.Task;

	public void Exit(int code) => _exited.TrySetResult(code);

	public void Kill()
	{
		owner.Killed();
		_killed.Cancel();
		ToClient.Complete();
		Exit(-1);
	}

	public void Dispose() { }
}

/// <summary>A one-way in-memory pipe: what is written is read in the same chunks; disposing the writer ends it.</summary>
internal sealed class ChannelStream : Stream
{
	private readonly Channel<byte[]> _chunks = Channel.CreateUnbounded<byte[]>();
	private byte[] _current = [];
	private int _offset;

	public void Push(byte[] chunk) => _chunks.Writer.TryWrite(chunk);

	public void WriteAndComplete(byte[] bytes)
	{
		Push(bytes);
		Complete();
	}

	public void Complete() => _chunks.Writer.TryComplete();

	public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
	{
		while (_offset == _current.Length)
		{
			if (!await _chunks.Reader.WaitToReadAsync(cancellationToken))
				return 0;
			if (_chunks.Reader.TryRead(out var next))
			{
				_current = next;
				_offset = 0;
			}
		}

		var count = Math.Min(buffer.Length, _current.Length - _offset);
		_current.AsMemory(_offset, count).CopyTo(buffer);
		_offset += count;
		return count;
	}

	public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

	public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
	{
		if (!_chunks.Writer.TryWrite(buffer.ToArray()))
			throw new IOException("the pipe is closed");
		return ValueTask.CompletedTask;
	}

	public override void Write(byte[] buffer, int offset, int count) => WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

	public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public override void Flush() { }

	protected override void Dispose(bool disposing)
	{
		Complete();
		base.Dispose(disposing);
	}

	public override bool CanRead => true;

	public override bool CanSeek => false;

	public override bool CanWrite => true;

	public override long Length => throw new NotSupportedException();

	public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

	public override void SetLength(long value) => throw new NotSupportedException();
}
