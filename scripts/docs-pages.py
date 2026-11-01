"""Patch the HTML pages docs-builder wrote, for scripts/docs-finish.sh.

    python3 -I scripts/docs-pages.py <output dir> <docs dir> <path prefix> <logo, relative to docs> <width> <height>

- Links to the site root load it whole (hx-boost="false"). The root is the landing page, which has none of the docs
  shell, so htmx's boosted swap of #main-container would find nothing to swap and leave the reader on the docs.
- The header shows the given logo (the landing page's animated lockup). The design-system header has a slot for a
  logo (site-header__logo) that docs-builder never fills, even with branding.icon set.
"""

import re
import sys
from pathlib import Path

out, docs, prefix, logo = Path(sys.argv[1]), Path(sys.argv[2]), sys.argv[3].strip("/"), sys.argv[4].strip("/")
width, height = int(sys.argv[5]), int(sys.argv[6])
root = f"/{prefix}/" if prefix else "/"
if not (docs / logo).is_file():
    print(f"docs: the logo {docs / logo} does not exist", file=sys.stderr)
    sys.exit(1)
logo_img = f'<img class="site-header__logo" src="{root}{logo}" alt="" width="{width}" height="{height}">'
brand = re.compile(r'(<a\b[^>]*\bclass="site-header__brand"[^>]*>)(?!\s*<img\b)')

root_link = re.compile(r'<a\b(?![^>]*\bhx-boost=)([^>]*\bhref="' + re.escape(root) + r'")')
any_root = re.compile(r'<a\b[^>]*\bhref="' + re.escape(root) + r'"')

pages = links = logos = 0
seen_root = seen_brand = False
for page in out.rglob("*.html"):
    html = page.read_text(encoding="utf-8")
    seen_root = seen_root or bool(any_root.search(html))
    seen_brand = seen_brand or 'class="site-header__brand"' in html
    new, n_links = root_link.subn(r'<a hx-boost="false"\1', html)
    new, n_logos = brand.subn(lambda m: m.group(1) + logo_img, new)
    if new != html:
        page.write_text(new, encoding="utf-8")
        pages += 1
        links += n_links
        logos += n_logos

if not seen_root:
    print(f'docs: no links to "{root}" found; the docs-builder markup changed', file=sys.stderr)
    sys.exit(1)
if not seen_brand:
    print("docs: no site-header__brand found for the logo; the docs-builder markup changed", file=sys.stderr)
    sys.exit(1)
print(f"docs: {pages} pages patched ({links} root links unboosted, {logos} logos)")
