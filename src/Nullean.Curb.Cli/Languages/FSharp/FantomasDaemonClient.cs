using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Nullean.Curb.Cli.Languages.FSharp;

/// <summary>What Fantomas made of one document.</summary>
internal enum FantomasOutcome
{
	Formatted,
	Unchanged,
	Error,
	Ignored,
}

/// <summary>One answer from the daemon.</summary>
/// <param name="Outcome">Its case.</param>
/// <param name="Content">The formatted text, for <see cref="FantomasOutcome.Formatted"/>.</param>
/// <param name="Message">What went wrong, for <see cref="FantomasOutcome.Error"/>.</param>
internal readonly record struct FantomasReply(FantomasOutcome Outcome, string? Content, string? Message);

/// <summary>The daemon went away, or answered something that is not the protocol.</summary>
internal sealed class FantomasDaemonException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// A JSON-RPC 2.0 client for <c>fantomas daemon</c>, written against the wire format directly so it stays
/// free of reflection and runs under Native AOT.
/// </summary>
/// <remarks>
/// <para>
/// The protocol is LSP's framing: each message is <c>Content-Length: N\r\n\r\n</c> followed by N bytes of
/// UTF-8 JSON. Fantomas answers <c>fantomas/version</c> with a string and
/// <c>fantomas/formatDocument</c> with an F# union as Newtonsoft serialises one,
/// <c>{"Case": …, "Fields": […]}</c>. Between them it sends <c>fantomas/configurationWarning</c>
/// notifications, which carry no id; this client reports their problems and otherwise ignores them.
/// </para>
/// <para>
/// Requests may be in flight concurrently. Writes are serialised; one reader task routes each response to
/// its request by id. Each message body is read into its own array, so nothing parsed from one can be
/// overwritten by the next.
/// </para>
/// </remarks>
internal sealed class FantomasDaemonClient : IAsyncDisposable
{
	private const string VersionMethod = "fantomas/version";
	private const string FormatDocumentMethod = "fantomas/formatDocument";
	private const string ConfigurationWarningMethod = "fantomas/configurationWarning";

	private static ReadOnlySpan<byte> HeaderEnd => "\r\n\r\n"u8;

	private readonly IToolProcess _process;
	private readonly Action<string>? _warn;
	private readonly SemaphoreSlim _writeLock = new(1, 1);
	private readonly ConcurrentDictionary<long, TaskCompletionSource<FantomasReply>> _pending = new();
	private readonly Task _readLoop;
	private long _nextId;
	private volatile Exception? _gone;

	/// <param name="process">A started daemon.</param>
	/// <param name="warn">Receives each configuration problem the daemon reports, worded for a person.</param>
	public FantomasDaemonClient(IToolProcess process, Action<string>? warn = null)
	{
		_process = process;
		_warn = warn;
		_readLoop = Task.Run(ReadLoopAsync);
	}

	/// <summary>The daemon's version, as Fantomas prints it, build metadata included.</summary>
	public async Task<string> VersionAsync(CancellationToken cancellationToken)
	{
		var reply = await SendAsync(VersionMethod, null, null, cancellationToken).ConfigureAwait(false);
		return reply.Content ?? throw new FantomasDaemonException("fantomas/version answered without a version");
	}

	/// <summary>Formats one document. <paramref name="path"/> must be absolute: Fantomas resolves its configuration and ignore file from it.</summary>
	public Task<FantomasReply> FormatAsync(string path, string source, CancellationToken cancellationToken) =>
		SendAsync(FormatDocumentMethod, path, source, cancellationToken);

	private async Task<FantomasReply> SendAsync(string method, string? path, string? source, CancellationToken cancellationToken)
	{
		if (_gone is { } gone)
			throw new FantomasDaemonException(gone.Message, gone);

		var id = Interlocked.Increment(ref _nextId);
		var completion = new TaskCompletionSource<FantomasReply>(TaskCreationOptions.RunContinuationsAsynchronously);
		_pending[id] = completion;

		// The reader may have failed every pending request between the check above and the add; a request
		// added after that would wait for an answer nobody is left to give.
		if (_gone is { } goneSince && _pending.TryRemove(id, out _))
			throw new FantomasDaemonException(goneSince.Message, goneSince);

		var body = new ArrayBufferWriter<byte>(source is null ? 128 : source.Length + 512);
		using (var writer = new Utf8JsonWriter(body))
		{
			writer.WriteStartObject();
			writer.WriteString("jsonrpc"u8, "2.0"u8);
			writer.WriteNumber("id"u8, id);
			writer.WriteString("method"u8, method);
			if (path is not null)
			{
				writer.WriteStartObject("params"u8);
				writer.WriteString("SourceCode"u8, source);
				writer.WriteString("FilePath"u8, path);
				writer.WriteNull("Config"u8);
				writer.WriteNull("Cursor"u8);
				writer.WriteEndObject();
			}
			writer.WriteEndObject();
		}

		var header = Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"Content-Length: {body.WrittenCount}\r\n\r\n"));

		await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			await _process.StandardInput.WriteAsync(header, cancellationToken).ConfigureAwait(false);
			await _process.StandardInput.WriteAsync(body.WrittenMemory, cancellationToken).ConfigureAwait(false);
			await _process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exception) when (exception is IOException or ObjectDisposedException)
		{
			_pending.TryRemove(id, out _);
			throw new FantomasDaemonException(DescribeExit("stopped accepting requests"), exception);
		}
		finally
		{
			_writeLock.Release();
		}

		using (cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken)))
			return await completion.Task.ConfigureAwait(false);
	}

	private async Task ReadLoopAsync()
	{
		var stream = _process.StandardOutput;
		var buffer = new byte[64 * 1024];
		var start = 0;
		var end = 0;
		try
		{
			while (true)
			{
				// Headers first: everything up to the blank line.
				int headerEnd;
				while ((headerEnd = buffer.AsSpan(start, end - start).IndexOf(HeaderEnd)) < 0)
				{
					if (start > 0)
					{
						Buffer.BlockCopy(buffer, start, buffer, 0, end - start);
						end -= start;
						start = 0;
					}

					if (end == buffer.Length)
						throw new FantomasDaemonException("the daemon sent a header longer than 64 KB");

					var read = await stream.ReadAsync(buffer.AsMemory(end)).ConfigureAwait(false);
					if (read == 0)
					{
						Fail(new FantomasDaemonException(DescribeExit(end == start ? "exited" : "exited mid-message")));
						return;
					}
					end += read;
				}

				var length = ContentLength(buffer.AsSpan(start, headerEnd));
				start += headerEnd + HeaderEnd.Length;

				// Then the body, into an array of its own: it is parsed after more of the stream has been
				// read into the shared buffer, and JsonDocument keeps a reference to what it was given.
				var body = new byte[length];
				var copied = Math.Min(length, end - start);
				Buffer.BlockCopy(buffer, start, body, 0, copied);
				start += copied;
				while (copied < length)
				{
					var read = await stream.ReadAsync(body.AsMemory(copied)).ConfigureAwait(false);
					if (read == 0)
					{
						Fail(new FantomasDaemonException(DescribeExit("exited mid-message")));
						return;
					}
					copied += read;
				}

				if (start == end)
					start = end = 0;

				Dispatch(body);
			}
		}
		catch (Exception exception)
		{
			Fail(exception as FantomasDaemonException ?? new FantomasDaemonException($"could not read from the daemon — {exception.Message}", exception));
		}
	}

	private static int ContentLength(ReadOnlySpan<byte> headers)
	{
		foreach (var range in headers.Split("\r\n"u8))
		{
			var line = headers[range];
			var colon = line.IndexOf((byte)':');
			if (colon < 0 || !Ascii.EqualsIgnoreCase(line[..colon].Trim((byte)' '), "Content-Length"u8))
				continue;

			if (int.TryParse(line[(colon + 1)..].Trim((byte)' '), NumberStyles.None, CultureInfo.InvariantCulture, out var length) && length >= 0)
				return length;
		}

		throw new FantomasDaemonException("the daemon sent a message without a Content-Length");
	}

	private void Dispatch(byte[] body)
	{
		using var document = JsonDocument.Parse(body);
		var root = document.RootElement;

		if (!root.TryGetProperty("id", out var idElement) || idElement.ValueKind == JsonValueKind.Null)
		{
			if (root.TryGetProperty("method", out var method) && method.ValueEquals(ConfigurationWarningMethod)
				&& root.TryGetProperty("params", out var parameters))
			{
				ReportConfigurationWarning(parameters);
			}
			return;
		}

		if (!idElement.TryGetInt64(out var id) || !_pending.TryRemove(id, out var completion))
			return;

		if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
		{
			var message = error.TryGetProperty("message", out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() : null;
			completion.TrySetException(new FantomasDaemonException($"the daemon refused the request — {message ?? "no message"}"));
			return;
		}

		if (!root.TryGetProperty("result", out var result))
		{
			completion.TrySetException(new FantomasDaemonException("the daemon answered without a result"));
			return;
		}

		try
		{
			completion.TrySetResult(ReadReply(result));
		}
		catch (FantomasDaemonException exception)
		{
			completion.TrySetException(exception);
		}
	}

	/// <summary>Reads a result: a bare string for the version, a union case for a document.</summary>
	internal static FantomasReply ReadReply(JsonElement result)
	{
		if (result.ValueKind == JsonValueKind.String)
			return new FantomasReply(FantomasOutcome.Unchanged, result.GetString(), null);

		if (result.ValueKind != JsonValueKind.Object
			|| !result.TryGetProperty("Case", out var caseName) || caseName.ValueKind != JsonValueKind.String)
		{
			throw new FantomasDaemonException("the daemon answered with something that is not a format result");
		}

		var fields = result.TryGetProperty("Fields", out var f) && f.ValueKind == JsonValueKind.Array ? f : default;
		string? Field(int index) =>
			fields.ValueKind == JsonValueKind.Array && fields.GetArrayLength() > index && fields[index].ValueKind == JsonValueKind.String
				? fields[index].GetString()
				: null;

		if (caseName.ValueEquals("Formatted"))
			return new FantomasReply(FantomasOutcome.Formatted, Field(1) ?? throw new FantomasDaemonException("Formatted arrived without its content"), null);
		if (caseName.ValueEquals("Unchanged"))
			return new FantomasReply(FantomasOutcome.Unchanged, null, null);
		if (caseName.ValueEquals("Error"))
			return new FantomasReply(FantomasOutcome.Error, null, Field(1) ?? "no message");
		if (caseName.ValueEquals("IgnoredFile"))
			return new FantomasReply(FantomasOutcome.Ignored, null, null);

		throw new FantomasDaemonException($"the daemon answered with an unknown case '{caseName.GetString()}'");
	}

	/// <summary>
	/// Reports the settings Fantomas could not act on. Sent after every request, usually with nothing in it.
	/// </summary>
	private void ReportConfigurationWarning(JsonElement parameters)
	{
		if (_warn is null)
			return;

		// StreamJsonRpc sends positional parameters: an array holding the one warning.
		var warning = parameters.ValueKind == JsonValueKind.Array && parameters.GetArrayLength() > 0 ? parameters[0] : parameters;
		if (warning.ValueKind != JsonValueKind.Object
			|| !warning.TryGetProperty("Problems", out var problems) || problems.ValueKind != JsonValueKind.Array)
		{
			return;
		}

		foreach (var problem in problems.EnumerateArray())
		{
			if (problem.ValueKind != JsonValueKind.Object)
				continue;

			var setting = problem.TryGetProperty("Setting", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
			var value = problem.TryGetProperty("Value", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
			if (setting is null)
				continue;

			_warn(value is null
				? $"Fantomas does not know the setting '{setting}'"
				: $"Fantomas does not recognise '{value}' for '{setting}'");
		}
	}

	private void Fail(Exception exception)
	{
		_gone ??= exception;
		foreach (var id in _pending.Keys)
		{
			if (_pending.TryRemove(id, out var completion))
				completion.TrySetException(exception);
		}
	}

	private string DescribeExit(string what)
	{
		var error = _process.StandardError.Trim();
		return error.Length == 0 ? $"the Fantomas daemon {what}" : $"the Fantomas daemon {what}: {FirstLine(error)}";
	}

	private static string FirstLine(string text)
	{
		var newline = text.IndexOfAny(['\r', '\n']);
		return newline < 0 ? text : text[..newline];
	}

	/// <summary>
	/// Closes the daemon's input, which is how it is told to stop, and waits a moment for it to; kills it if
	/// it does not.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		try
		{
			await _writeLock.WaitAsync().ConfigureAwait(false);
			_process.StandardInput.Dispose();
		}
		catch (Exception exception) when (exception is IOException or ObjectDisposedException)
		{
			// Already gone.
		}

		try
		{
			await _process.Exited.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
		}
		catch (TimeoutException)
		{
			_process.Kill();
		}

		try
		{
			await _readLoop.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
		}
		catch (TimeoutException)
		{
			// The pipe is held by something else; nothing more to read from it that matters.
		}

		Fail(new FantomasDaemonException("the client was disposed"));
		_process.Dispose();
		_writeLock.Dispose();
	}
}
