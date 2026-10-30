---
navigation_title: Razor code blocks (experimental)
description: An experimental, opt-in mode that formats the C# inside @code and @functions blocks of .razor and .cshtml files and leaves every other byte of the file as it is.
---

# Razor code blocks (experimental)

{{product}} can format the C# in the `@code` and `@functions` blocks of your `.razor` components and
`.cshtml` views with the same rules and the same `.editorconfig` keys as your `.cs` files. Nothing else in
the file changes: markup, directives, `@{ }` blocks and expressions stay exactly as you wrote them. It's
experimental and off by default.

## Turning it on

Add the key to a section that names Razor files:

```ini
[*.{razor,cshtml}]
curb_razor_format_code_blocks = true
```

The key counts only in a section whose glob names an extension or a file name, such as `[*.razor]`,
`[*.{cs,razor,cshtml}]` or `[Pages/**/*.razor]`. In `[*]`, `[**]` or `[*.*]` it is ignored, so a catch-all
section can't switch on an experimental feature by accident. A more specific section can turn it off again:

```ini
[Legacy.razor]
curb_razor_format_code_blocks = false
```

Without the key, `curb format` and `curb check` never read a `.razor` or `.cshtml` file and don't count
them. A file you name yourself (`curb check Pages/Counter.razor`) is skipped with one line saying Razor
isn't enabled for it. `--language razor` formats Razor files alone, and still only those that opt in. The
`curb` MSBuild package never formats Razor files.

## Which C# style applies

A block is formatted with the C# options bound from the Razor file's own settings, exactly as a `.cs`
file's are. To give both the same style, put your C# keys in a section that covers both:

```ini
[*.{cs,razor,cshtml}]
indent_size = 4
csharp_new_line_before_open_brace = all
```

Blazor components often write `[Parameter] public int Count { get; set; }` on one line. {{product}} moves the
attribute to its own line, as it does in a `.cs` file and as `dotnet format` does. With `max_line_length` set,
`csharp_place_property_attribute_on_same_line = if_owner_is_single_line` keeps the one-line shape.

Three settings work differently from a `.cs` file, because the markup around a block is never rewritten:

| Setting | In a Razor file |
|---|---|
| `end_of_line` | Ignored. A block keeps the line endings of the line that opens it, so a file never ends up with two kinds. |
| `charset` | Ignored. A byte-order mark stays as the file has it. |
| `file_header_template` | Ignored. A header belongs at the top of a `.cs` file, not inside a block. |

A block that is indented (inside an element, say) keeps its column. Each member line starts with the `@code`
line's own leading whitespace exactly as written, followed by one level of indentation in your
`indent_style`. A tab-indented `@code` with `indent_style = space` gives `\t    int x;`, and a
two-space-indented one with `indent_style = tab` gives `  \tint x;`. `max_line_length` is measured from the
start of the line.

## What gets formatted

A block is a line whose first text is `@code` or `@functions`, then `{` and the end of the line (the `{`
may also stand alone on the next line), up to the `}` that closes it on a line of its own:

```html
<h1>Counter</h1>

@code {
  private int currentCount=0;

      private void IncrementCount()
  {
    currentCount+=1;
  }
}
```

becomes

```html
<h1>Counter</h1>

@code {
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount += 1;
    }
}
```

Only the lines between the `{` line and the `}` line are replaced. The `{` line, the `}` line and every
byte outside them are copied from the source.

## What it never touches

- Markup, attributes, `@page`, `@using`, `@inject` and every other directive.
- `@{ }` blocks, `@if`, `@foreach`, `@switch` and the other code constructs inside markup.
- Implicit and explicit expressions, such as `@count` and `@(a + b)`.
- `@code` inside a Razor comment (`@* *@`) or an HTML comment (`<!-- -->`), and `@@code`, which is text.
- A block on one line, such as `@code { int x; }`.
- `@code` in a `.cshtml` view. It's a directive in components only; in a view it's an expression followed
  by markup. Views get their `@functions` blocks formatted.

## Where blocks are looked for

Blocks are looked for in markup only. Every region Razor parses as C# is skipped whole, with its end found by
the same C# lexer that matches a block's braces:

- `@{ }` and `@( )`;
- an implicit expression with its member accesses, calls and indexers, such as `@Html.Raw(…)` or `@items[…]`;
- `@if`/`else`, `@for`, `@foreach`, `@while`, `@do … while`, `@switch`, `@using ( )`, `@lock` and
  `@try`/`catch`/`finally`, with their bodies;
- a `@code` or `@functions` block that isn't formatted, such as one with a comment after its `{`.

So a line reading `@code {` inside a string, a comment or a body in any of these is never taken for a block.

If {{product}} can't find where such a region ends (an unclosed `@{`, or `@if` without braces), it stops
looking: no block after that point in the file is formatted. The same goes for a `@code` block with no closing
brace.

## When a block is left as written

{{product}} refuses a block it can't format safely, leaves it exactly as it is, and still formats the other
blocks in the file. The run reports each refusal with its line and reason, and counts them in the summary:

```text
Components/Pages/Grid.razor: line 41: @code block left as written, it does not parse as C# class members (Invalid expression term '<')
…
  razor: 58 file(s) — 5 changed, 0 failed, 0 unparsable, 1 block(s) left as written
```

| Reason | Why |
|---|---|
| it does not parse as C# class members | The block holds Razor rather than C#: a template (`@<p>…</p>`), `<text>`, `@:`, a Razor comment, or markup in a method body. {{product}} doesn't parse Razor. |
| its closing brace is not alone on its line | Something shares the line with the `}` that closes the block, such as `} <p>…` or a stray `};`. |
| no closing brace | The C# lexer never finds the `}` that closes the block. |
| it nests too deeply | The same limit as a `.cs` file. |
| formatting moved the braces around it | The C# options joined the braces of the class the block is formatted in. |

A refused block isn't a failure: the exit code doesn't change, and a file with one is never cached, so the
refusal is reported again on the next run.

## How it stays safe

- The braces are matched with the C# compiler's own lexer, so a `{` or `}` in a string (verbatim,
  interpolated or raw), a character literal or a comment never ends a block early.
- A block's text is formatted as the members of a class, with every check a `.cs` file gets: the content
  verifier and the token-stream comparison.
- A multi-line string, the inside of a multi-line comment and an inactive `#if` branch keep their lines
  exactly when the block is re-indented.
- Before anything is written, {{product}} scans the output again and requires the same blocks, every byte
  outside them identical to the source, and each block holding exactly the formatted text. If any check
  fails, the whole file is reported as failed and left as it was.

## Measured

Each repository was formatted with the key forced on, then formatted a second time. A separate script with
its own block finder masked every block's inner lines and compared the rest of each file byte for byte.

| Repository | Razor files | Blocks | Blocks changed | Left as written | Failed | Markup changed | Second pass |
|---|---|---|---|---|---|---|---|
| dotnet/aspnetcore (`src/Components`, `src/Mvc`) | 1,260 | 563 | 190 | 5 | 0 | 0 | no change |
| dotnet/blazor-samples | 3,091 | 2,296 | 427 | 16 | 0 | 0 | no change |
| dotnet/eShop | 58 | 23 | 5 | 0 | 0 | 0 | no change |
| elastic/docs-builder (`.cshtml` views) | 242 | 41 | 4 | 13 | 0 | 0 | no change |

Every change in those runs was whitespace. Every refusal was a block holding Razor markup (a template, or
markup in a method body), except two copies of a sample with a stray `};` after its last member.

With the key absent, a repository's Razor files cost almost nothing: about 3 ms on dotnet/blazor-samples'
3,080 components, the price of looking for the key in each folder's `.editorconfig` files.

## Limitations

- Experimental. The output may change between releases.
- Only `@code` and `@functions` blocks. There are no plans to format markup.
- `print-config` and `doc-tree` work on `.cs` files only.
- The C# lexer reads the markup inside an `@if` or `@foreach` body as C#, so a brace in that markup's text
  (`<p>{</p>`) is counted. The body's end is then not found, and nothing after it in the file is formatted.

See the [option reference](../reference/razor/code-blocks/curb_razor_format_code_blocks.md) for the key.
