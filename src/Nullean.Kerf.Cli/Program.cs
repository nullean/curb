using Nullean.Argh;
using Nullean.Kerf.Cli;

// KerfCommands' public methods become the command tree; see AGENTS.md.
var app = new ArghApp();
app.UseCliDescription("kerf — a C# formatter driven by your .editorconfig");
app.Map<KerfCommands>();

return await app.RunAsync(args);
