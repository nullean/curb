using Nullean.Argh;
using Nullean.Curb.Cli;

// CurbCommands' public methods become the command tree; see AGENTS.md.
var app = new ArghApp();
app.UseCliDescription("curb — a C# formatter driven by your .editorconfig");
app.Map<CurbCommands>();

return await app.RunAsync(args);
