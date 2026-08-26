---
navigation_title: Conformance divergences
description: Every case where Curb's output is not a fixed point of dotnet format or jb cleanupcode at all, and why no shape it could choose would be one.
---

# Conformance divergences

This page lists every known case where {{product}}'s output `Z` is **not a fixed point** of a reference
tool at all — `T(Z) != Z` — because the tool cannot recognise what {{product}} did, so no shape Curb could
have chosen instead would fix it. See [conformance](conformance.md#when-tz--z-cannot-hold-at-all) for what
that means and how it differs from the ordinary, unlisted case of a fixed point in a different shape than
the tool's own (`X != Z` with `T(Z) == Z` still holding — expected, common, and reported only in aggregate
by `./build.sh verifyexpectations`, not itemised here). Each row below is a deliberate, investigated choice,
not an oversight — an undocumented `T(Z) != Z` fails CI.

The table is sourced from `build/conformance-divergences.json`, the registry `./build.sh verifyexpectations`
checks a discovered non-fixed-point against. Until `./build.sh options` generates this page from that file
(tracked separately), keep the two in sync by hand when either changes.

| Option key | Tool | Fixed point? | Why no shape survives | Case |
|---|---|---|---|---|
| `csharp_empty_block_style` | `dotnet format whitespace` | No | A ReSharper-only key `dotnet format` does not recognise at all — inert on its own. The divergence only shows up combined with `csharp_preserve_single_line_blocks = false`, an IDE0055 key `dotnet format` understands as "expand every single-line block," including the empty one Curb just recollapsed. The two are in permanent, direct conflict whenever both are set this way — no shape survives both at once. | `EmptyBlockStyleTests.Together_ignores_preserve_single_line_blocks_being_off` |
| `csharp_indent_labels` | `dotnet format whitespace` | No | Curb falls back to the documented default (`one_less_than_current`) for an unrecognised value. `dotnet format`'s own fallback for the same unrecognised value does not match its documented default either (it produces the `no_change` shape instead), so the two fallbacks disagree. | `IndentationTests.An_unrecognised_label_value_falls_back_to_the_default` |
| `csharp_new_line_before_open_brace` | `dotnet format whitespace` | No | Same fallback-mismatch shape as `csharp_indent_labels`: Curb falls back to the documented default (`all`). `dotnet format` leaves the source untouched for the same unrecognised value — it silently skips applying the setting rather than falling back to its own documented default. | `NewLineBeforeOpenBraceTests.An_unrecognised_value_falls_back_to_the_default` |
| `#pragma warning disable` (bare, or naming `IDE0055`) — not an `.editorconfig` key | `dotnet format whitespace` | No | Curb leaves a suppressed region unformatted, matching what IDE0055 itself would do. `dotnet format whitespace` is not diagnostic-driven — a plain Roslyn formatter pass with no concept of pragma suppression — so it always reformats the region regardless. Structural and permanent for any suppressed region that isn't already `dotnet format`-clean on its own. | `SuppressionTests.A_bare_disable_covers_every_rule_including_this_one` |
| `tab_width` | `dotnet format whitespace` | No | Measured on `indent_style = tab` + `tab_width = 8` + a width narrow enough to force wrapping: `dotnet format` converts the outer two levels of tab indentation to four-space equivalents while leaving the wrapped argument lines as tabs — inconsistent, and only reproduced in this exact combination. Not fully root-caused; recorded as observed. `dotnet format` has nothing to check reflow width against otherwise, since it does not reflow at all. | `CoreOptionTests.Tab_width_counts_toward_the_line_length` |
