namespace Smoke;

// Already formatted, and it stays that way. Program.cs is restored to its misformatted state before
// every build in the harness, so on its own the sample could never show a cache hit — the one file it
// had would miss every time. This is the file the cache is expected to serve.
public static class Stable
{
    public static int Twice(int x)
    {
        return x + x;
    }
}
