using System.Buffers;

namespace Nullean.Curb.Printing;

/// <summary>
/// The pooled character buffer the printer writes into.
/// </summary>
/// <remarks>
/// Trailing-whitespace trimming is a length rewind rather than a copy, and the result is exposed as
/// a span so <c>check</c> can compare against the source without materialising a string — the common
/// case on a repository where almost nothing needs changing.
/// </remarks>
internal sealed class OutputBuffer : IDisposable
{
	private char[] _buffer;
	private int _length;

	public OutputBuffer(int capacity = 4096) => _buffer = ArrayPool<char>.Shared.Rent(Math.Max(capacity, 256));

	public int Length => _length;

	public ReadOnlySpan<char> Written => _buffer.AsSpan(0, _length);

	public void Reset(int capacity = 0)
	{
		_length = 0;
		if (capacity > _buffer.Length)
			Grow(capacity);
	}

	public void Append(ReadOnlySpan<char> value)
	{
		if (_length + value.Length > _buffer.Length)
			Grow(_length + value.Length);
		value.CopyTo(_buffer.AsSpan(_length));
		_length += value.Length;
	}

	public void Append(char value)
	{
		if (_length == _buffer.Length)
			Grow(_length + 1);
		_buffer[_length++] = value;
	}

	/// <summary>The last character written, or NUL when nothing has been.</summary>
	public char LastChar() => _length == 0 ? '\0' : _buffer[_length - 1];

	/// <summary>True when only spaces and tabs separate the end of the buffer from a line start.</summary>
	public bool AtLineStart()
	{
		for (var i = _length - 1; i >= 0; i--)
		{
			if (_buffer[i] is ' ' or '\t')
				continue;
			return _buffer[i] is '\n';
		}
		return true;
	}

	/// <summary>Removes spaces and tabs from the end of the buffer, returning how many were removed.</summary>
	public int TrimTrailingWhitespace()
	{
		var trimmed = 0;
		while (_length > 0 && _buffer[_length - 1] is ' ' or '\t')
		{
			_length--;
			trimmed++;
		}
		return trimmed;
	}

	/// <summary>Rewinds to exactly one trailing newline, then guarantees it.</summary>
	public void EnsureSingleTrailingNewLine(string endOfLine)
	{
		while (_length > 0 && _buffer[_length - 1] is '\n' or '\r')
			_length--;
		Append(endOfLine);
	}

	/// <summary>Removes every trailing newline, for files that must not end in one.</summary>
	public void RemoveTrailingNewLines()
	{
		while (_length > 0 && _buffer[_length - 1] is '\n' or '\r')
			_length--;
	}

	public override string ToString() => new(_buffer, 0, _length);

	private void Grow(int capacity)
	{
		var next = ArrayPool<char>.Shared.Rent(Math.Max(capacity, _buffer.Length * 2));
		_buffer.AsSpan(0, _length).CopyTo(next);
		ArrayPool<char>.Shared.Return(_buffer);
		_buffer = next;
	}

	public void Dispose()
	{
		if (_buffer.Length <= 0)
			return;
		ArrayPool<char>.Shared.Return(_buffer);
		_buffer = [];
		_length = 0;
	}
}
