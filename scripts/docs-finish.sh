#!/usr/bin/env bash
# What every docs build (scripts/docs.sh, ./build.sh docs and .github/workflows/docs.yml) does after docs-builder: the
# landing page over the generated index.html, the files docs-builder doesn't copy, and the Curb theme appended to the
# design system's stylesheet. Needs a build with FEATURE_NAVIGATION_PREVIEW=true: without it the pages load no ds.css
# and this fails.
#
#   scripts/docs-finish.sh .artifacts/docs/html [path prefix]
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
out="${1:?usage: docs-finish.sh <docs-builder output dir> [path prefix]}"
prefix="${2:-}"

# The theme goes into the ds.css the pages actually load. Its name comes from docs-builder (plain ds.css with ?v=, or a
# hashed file), so read it from a built page instead of guessing. Before the landing page replaces index.html.
probe="$out/getting-started/index.html"
[[ -f "$probe" ]] || { echo "docs: $probe is missing, cannot find the design-system stylesheet" >&2; exit 1; }
href="$(grep -o 'href="[^"]*/_static/ds[^"/]*\.css[^"]*"' "$probe" | grep -v 'ds-layers' | head -n1 | sed 's/^href="//; s/"$//; s/?.*$//')" || true
if [[ -z "$href" ]]; then
  echo "docs: $probe loads no design-system ds.css; build with FEATURE_NAVIGATION_PREVIEW=true" >&2
  exit 1
fi
css="$out/_static/${href##*/_static/}"
[[ -f "$css" ]] || { echo "docs: $css (from $href) does not exist" >&2; exit 1; }
# An incremental docs-builder build keeps the previous ds.css, theme and all: cut it at the marker before appending.
marker="/* curb-docs.css: appended by scripts/docs-finish.sh */"
awk -v m="$marker" '$0 == m { exit } { print }' "$css" > "$css.tmp"
{ printf '%s\n' "$marker"; cat "$root/docs/curb-docs.css"; } >> "$css.tmp"
mv "$css.tmp" "$css"

# Root links that skip htmx, and the header logo: see scripts/docs-pages.py. The logo is the landing page's animated
# lockup (docs/images/curb-lockup-nav.svg, 88x48 as in the landing header), not branding.icon: docs-builder uses that
# square mark elsewhere (the footer), where a wide lockup with its own wordmark would not fit.
python3 -I "$root/scripts/docs-pages.py" "$out" "$root/docs" "$prefix" images/curb-lockup-nav.svg 88 48

cp "$root/docs/curb-landing.html" "$out/index.html"
cp "$root/docs/robots.txt" "$out/robots.txt"
# docs-builder copies images, not fonts; the theme needs these (it ships Inter and JetBrains Mono itself).
mkdir -p "$out/fonts" && cp "$root"/docs/fonts/*.woff2 "$out/fonts/"
echo "docs: finished $out (theme appended to ${css#"$out"/}, landing page applied)"
