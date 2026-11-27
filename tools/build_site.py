#!/usr/bin/env python3
"""Builds the whole website (index, one page per killer, site.css) from the game's own killer menu.

Usage:  python build_site.py "<path>\\mechanic_classes\\lua\\autorun\\client\\cl_killer_menu.lua" <output folder>

Everything on the killer pages (names, roles, difficulty, stats, abilities, overview) is read from
cl_killer_menu.lua, so the site always matches the in-game menu. Spinning models are optional video files
named models/<slug>.webm (and .mp4) inside the output folder; a killer without one shows a big initial.
"""
import html, os, re, sys
from lupa import LuaRuntime

DISCORD = "https://discord.gg/nKmsM5a2TQ"
INSTALLER = "FightOrFlight_Installer.exe"
SITE_NAME = "Fight or Flight : Hide N Seek"
LEDE = ("Fight or Flight : Hide n Seek is a game mode that adds various mechanics to the standard Gmod Hide n Seek experience! "
        "Most notable are the classes for hiders, killer rebalancing, various movement mechanics, and balancing of the game mode overall.")


def grab(src, name):
    m = re.search(r"^local %s\s*=\s*\{" % name, src, re.M)
    if not m:
        sys.exit("table %s not found" % name)
    i = m.end() - 1
    end = src.index("\n}", i) + 2
    return src[i:end]


def load_tables(path):
    src = open(path, encoding="utf-8", errors="replace").read()
    lua = LuaRuntime(unpack_returned_tuples=True)
    lua.execute('setmetatable(_G, {__index = function() return "x" end})')
    out = {}
    for name in ("KILLER_DIFFICULTY", "KILLER_ROLES", "KILLER_DESCRIPTIONS", "KILLER_INFOGRAPHICS"):
        out[name] = lua.eval("(function() return %s end)()" % grab(src, name))
    return out


def to_py(t):
    if hasattr(t, "items"):
        keys = list(t.keys())
        if keys and all(isinstance(k, int) for k in keys):
            return [to_py(t[k]) for k in sorted(keys)]
        return {k: to_py(v) for k, v in t.items()}
    return t


def slug(name):
    s = name.replace(".", "")
    s = re.sub(r"[^A-Za-z0-9]+", "-", s).strip("-").lower()
    return re.sub(r"^the-", "", s)


def esc(s):
    return html.escape(str(s), quote=True)


def clean(s):
    return re.sub(r"[ \t]+", " ", str(s)).strip()


CSS = """/* Layout: one dark column. Huge condensed wordmark, killer cards, a stage for the spinning model. */
:root {
  --bg: #120f0f;
  --panel: #221c1c;
  --panel-hot: #322929;
  --line: #423636;
  --red: #8c1414;
  --red-hot: #b41e1e;
  --accent: #f05a5a;
  --text: #ebebeb;
  --muted: #a39a9a;
  --ability: #ff2828;
  --stat: #5ae66e;
  --star: #ffc828;
  --star-off: #504b4b;
  --role-executioner: #eb4646;
  --role-pursuer: #ff7319;
  --role-anomaly: #78ff32;
  --display: "Big Shoulders Display", "Arial Narrow", Impact, sans-serif;
  --body: "Inter", system-ui, -apple-system, "Segoe UI", sans-serif;
  color-scheme: dark;
}
* { box-sizing: border-box; }
html { background: var(--bg); scroll-behavior: smooth; }
body {
  margin: 0;
  background: var(--bg);
  color: var(--text);
  font: 400 16px/1.6 var(--body);
  padding-left: max(16px, env(safe-area-inset-left, 0px));
  padding-right: max(16px, env(safe-area-inset-right, 0px));
}
a { color: inherit; }
.wrap { max-width: 980px; margin: 0 auto; }
:focus-visible { outline: 2px solid var(--accent); outline-offset: 3px; }
code { font-size: .9em; background: var(--panel); padding: 1px 6px; border-radius: 3px; }

.top { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding-block: 20px; }
.mark { font: 800 1.15rem/1 var(--display); letter-spacing: .08em; text-transform: uppercase; text-decoration: none; }
.mark span { color: var(--accent); }
.top a.link { font-size: .9rem; color: var(--muted); text-decoration: none; border-bottom: 1px solid var(--line); padding-bottom: 2px; }
.top a.link:hover { color: var(--text); border-color: var(--accent); }

.hero { padding-block: 56px 64px; border-top: 1px solid var(--line); }
.hero h1 { margin: 0; font: 900 clamp(3.4rem, 15vw, 9.5rem)/.86 var(--display); text-transform: uppercase; letter-spacing: -.01em; text-wrap: balance; }
.hero h1 .or { color: var(--accent); }
.hero .sub { margin: 18px 0 0; font: 700 clamp(1.3rem, 4.5vw, 2.1rem)/1 var(--display); letter-spacing: .24em; text-transform: uppercase; color: var(--muted); }
.hero p.lede { max-width: 56ch; margin: 28px 0 0; font-size: 1.1rem; color: #cfc8c8; }
.cta { display: flex; flex-wrap: wrap; gap: 12px; margin-top: 32px; }
.btn { display: inline-flex; align-items: center; min-height: 48px; padding: 0 26px; border-radius: 4px; font: 600 .95rem/1 var(--body); letter-spacing: .06em; text-transform: uppercase; text-decoration: none; border: 1px solid var(--line); background: var(--panel); transition: background .15s, border-color .15s; }
.btn:hover { background: var(--panel-hot); border-color: var(--accent); }
.btn.primary { background: var(--red); border-color: var(--red-hot); }
.btn.primary:hover { background: var(--red-hot); }

section { padding-block: 56px; border-top: 1px solid var(--line); }
h2 { margin: 0 0 8px; font: 800 clamp(1.9rem, 6vw, 2.8rem)/1 var(--display); text-transform: uppercase; letter-spacing: .02em; }
h2 + p { margin: 0 0 28px; max-width: 58ch; color: var(--muted); }

.roster { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(190px, 1fr)); gap: 8px; }
.roster li { min-width: 0; }
.roster a { display: flex; flex-direction: column; gap: 6px; height: 100%; padding: 16px 16px 14px; background: var(--panel); border: 1px solid var(--line); border-radius: 4px; text-decoration: none; transition: border-color .15s, background .15s; }
.roster a:hover { background: var(--panel-hot); border-color: var(--red-hot); }
.roster .nm { font: 700 1.25rem/1.1 var(--display); letter-spacing: .06em; text-transform: uppercase; overflow-wrap: anywhere; }
.tag { font: 600 .68rem/1 var(--body); letter-spacing: .12em; text-transform: uppercase; }
.tag.executioner { color: var(--role-executioner); }
.tag.pursuer { color: var(--role-pursuer); }
.tag.anomaly { color: var(--role-anomaly); }

.steps { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; counter-reset: step; }
.steps li { counter-increment: step; display: grid; grid-template-columns: 56px 1fr; gap: 16px; align-items: start; padding: 20px; background: var(--panel); border: 1px solid var(--line); border-radius: 4px; }
.steps li::before { content: counter(step); font: 900 2.6rem/.9 var(--display); color: var(--accent); }
.steps b { display: block; margin-bottom: 2px; font: 700 1.3rem/1.15 var(--display); letter-spacing: .05em; text-transform: uppercase; }
.steps span { color: var(--muted); }
.note { margin: 20px 0 0; font-size: .9rem; color: var(--muted); max-width: 64ch; }
.pagehead { padding-block: 40px 8px; }
.pagehead h1 { margin: 0 0 12px; font: 900 clamp(2.6rem, 9vw, 5rem)/.9 var(--display); text-transform: uppercase; letter-spacing: -.01em; }
.pagehead p { margin: 0; max-width: 60ch; color: #cfc8c8; font-size: 1.05rem; }
.backlink { display: inline-block; margin-top: 8px; font-size: .9rem; color: var(--muted); text-decoration: none; border-bottom: 1px solid var(--line); padding-bottom: 1px; }
.backlink:hover { color: var(--text); border-color: var(--accent); }
.whylink { margin: 14px 0 0; font-size: .9rem; color: var(--muted); }
.whylink a { color: var(--text); text-decoration: none; border-bottom: 1px solid var(--line); padding-bottom: 1px; }
.whylink a:hover { border-color: var(--accent); }
.why { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.why li { padding: 16px 20px; background: var(--panel); border: 1px solid var(--line); border-radius: 4px; color: var(--muted); }
.why b { display: block; margin-bottom: 2px; font: 700 1.2rem/1.15 var(--display); letter-spacing: .05em; text-transform: uppercase; color: var(--text); }

footer { padding-block: 32px 48px; border-top: 1px solid var(--line); color: var(--muted); font-size: .85rem; }
footer p { margin: 0; max-width: 70ch; }

/* killer page */
.crumb { display: inline-block; margin-bottom: 20px; font-size: .9rem; color: var(--muted); text-decoration: none; border-bottom: 1px solid var(--line); padding-bottom: 2px; }
.crumb:hover { color: var(--text); border-color: var(--accent); }
.k { display: grid; grid-template-columns: minmax(0, 5fr) minmax(0, 6fr); gap: 32px; padding-block: 40px 56px; border-top: 1px solid var(--line); align-items: start; }
.stage { position: sticky; top: 16px; aspect-ratio: 1 / 1; background: var(--bg); border: 1px solid var(--line); border-radius: 4px; overflow: hidden; display: grid; place-items: center;
  background-image: radial-gradient(ellipse at 50% 85%, #3a1212 0%, #1a1212 45%, #120f0f 75%); }
.stage .initial { font: 900 clamp(8rem, 28vw, 15rem)/1 var(--display); color: var(--panel-hot); user-select: none; }
.stage video { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; }
.stage video[hidden] { display: none; }
.k h1 { margin: 0; font: 900 clamp(2.6rem, 9vw, 4.6rem)/.92 var(--display); text-transform: uppercase; letter-spacing: .01em; overflow-wrap: anywhere; }
.meta { display: flex; flex-wrap: wrap; align-items: center; gap: 14px; margin-top: 12px; }
.stars { font-size: 1.15rem; letter-spacing: .12em; color: var(--star-off); }
.stars b { font-weight: 400; color: var(--star); }
.stars small { margin-left: 6px; font: 600 .68rem/1 var(--body); letter-spacing: .12em; text-transform: uppercase; color: var(--muted); }
.tagline { margin: 22px 0 0; font: 700 clamp(1.3rem, 4vw, 1.8rem)/1.15 var(--display); letter-spacing: .04em; text-transform: uppercase; color: #cfc8c8; }
.stats { display: flex; flex-wrap: wrap; gap: 8px; margin: 24px 0 0; padding: 0; list-style: none; }
.stats li { padding: 10px 16px; background: var(--panel); border: 1px solid var(--line); border-radius: 4px; }
.stats span { display: block; font: 600 .66rem/1 var(--body); letter-spacing: .14em; text-transform: uppercase; color: var(--muted); }
.stats b { display: block; margin-top: 4px; font: 800 1.5rem/1 var(--display); letter-spacing: .04em; color: var(--stat); }
.k h2.abil { margin: 36px 0 12px; font-size: 1.6rem; }
.abilities { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.abilities li { padding: 16px 18px; background: var(--panel); border: 1px solid var(--line); border-radius: 4px; }
.abilities .an { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; font: 700 1.3rem/1.1 var(--display); letter-spacing: .05em; text-transform: uppercase; color: var(--ability); }
.key { font: 600 .68rem/1 var(--body); letter-spacing: .1em; padding: 5px 8px; border: 1px solid var(--line); border-radius: 3px; background: var(--bg); color: var(--text); }
.key.passive { color: var(--muted); }
.abilities p { margin: 6px 0 0; color: #cfc8c8; }
.over { margin: 36px 0 0; max-width: 60ch; color: var(--muted); }
.over p { margin: 0 0 12px; }
.pager { display: flex; justify-content: space-between; gap: 12px; flex-wrap: wrap; padding-block: 28px 8px; border-top: 1px solid var(--line); }
.pager a { display: flex; flex-direction: column; gap: 4px; min-width: 0; text-decoration: none; padding: 14px 18px; background: var(--panel); border: 1px solid var(--line); border-radius: 4px; transition: border-color .15s, background .15s; }
.pager a:hover { background: var(--panel-hot); border-color: var(--red-hot); }
.pager small { font: 600 .66rem/1 var(--body); letter-spacing: .14em; text-transform: uppercase; color: var(--muted); }
.pager b { font: 700 1.2rem/1.1 var(--display); letter-spacing: .06em; text-transform: uppercase; }
.pager .next { text-align: right; margin-left: auto; }

@media (max-width: 760px) {
  .k { grid-template-columns: minmax(0, 1fr); gap: 24px; }
  .stage { position: relative; top: 0; aspect-ratio: 1 / 1; }
}
@media (max-width: 520px) {
  .steps li { grid-template-columns: 40px 1fr; padding: 16px; }
  .btn { width: 100%; justify-content: center; }
  .roster { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .roster a { padding: 14px 12px; }
  .roster .nm { font-size: 1.05rem; }
  .pager a { width: 100%; }
  .pager .next { text-align: left; }
}
@media (prefers-reduced-motion: reduce) { * { transition: none !important; scroll-behavior: auto !important; } }
"""


def head(title, desc, css, root, canonical_path=""):
    return f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<title>{esc(title)}</title>
<meta name="description" content="{esc(desc)}">
<meta property="og:title" content="{esc(title)}">
<meta property="og:description" content="{esc(desc)}">
<meta property="og:type" content="website">
<meta name="theme-color" content="#120f0f">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Big+Shoulders+Display:wght@700;800;900&family=Inter:wght@400;500;600&display=swap">
<link rel="stylesheet" href="{root}site.css">
</head>
<body>
<div class="wrap">
  <header class="top">
    <a class="mark" href="{root}index.html">Fight or Flight <span>:</span> Hide N Seek</a>
    <a class="link" href="{DISCORD}" target="_blank" rel="noopener">Discord</a>
  </header>
"""


def foot():
    return """
  <footer>
    <p>Fight or Flight : Hide N Seek is a fan-made gamemode for Garry's Mod. Killer names belong to their respective owners and are used here only to describe the content.</p>
  </footer>
</div>
</body>
</html>
"""


def role_tag(role):
    return f'<span class="tag {esc(role.lower())}">{esc(role)}</span>' if role else ""


def build(menu_path, out):
    T = load_tables(menu_path)
    desc = to_py(T["KILLER_DESCRIPTIONS"])
    roles = to_py(T["KILLER_ROLES"])
    diff = to_py(T["KILLER_DIFFICULTY"])
    info = to_py(T["KILLER_INFOGRAPHICS"])
    names = sorted(desc.keys(), key=lambda n: slug(n))
    os.makedirs(os.path.join(out, "killers"), exist_ok=True)
    os.makedirs(os.path.join(out, "models"), exist_ok=True)
    open(os.path.join(out, "site.css"), "w", encoding="utf-8").write(CSS)

    # ---------------- index
    cards = "\n".join(
        f'        <li><a href="killers/{slug(n)}.html"><span class="nm">{esc(n)}</span>{role_tag(roles.get(n))}</a></li>'
        for n in names)
    index = head(SITE_NAME, LEDE, CSS, "") + f"""
  <main>
    <div class="hero">
      <h1>Fight <span class="or">or</span> Flight</h1>
      <p class="sub">Hide N Seek</p>
      <p class="lede">{esc(LEDE)}</p>
      <div class="cta">
        <a class="btn primary" href="{INSTALLER}">Download installer</a>
        <a class="btn" href="{DISCORD}" target="_blank" rel="noopener">Join the Discord</a>
        <a class="btn" href="#install">How to install</a>
      </div>
      <p class="whylink">Wondering why there's an installer? <a href="why.html">Here's why</a>.</p>
    </div>

    <section id="killers">
      <h2>The killers</h2>
      <p>{len(names)} killers to choose from, each one playing differently. Pick one to see their abilities.</p>
      <ul class="roster">
{cards}
      </ul>
    </section>

    <section id="install">
      <h2>Install it</h2>
      <p>The installer puts every addon straight into your Garry's Mod folder. It takes about a minute.</p>
      <ol class="steps">
        <li><div><b>Download the installer</b><span>Press Download installer on this page. Open <code>{INSTALLER}</code> from your Downloads folder. It works from anywhere.</span></div></li>
        <li><div><b>Press Install / Update</b><span>It finds your Garry's Mod folder on its own. If it can't, press Change and pick the <code>garrysmod\\addons</code> folder.</span></div></li>
        <li><div><b>Launch the game</b><span>When the bar finishes, press Launch Garry's Mod. To update later, open the installer again and press Install / Update.</span></div></li>
      </ol>
      <div class="cta"><a class="btn primary" href="{INSTALLER}">Download installer</a></div>
      <p class="note">Windows may warn that the installer is from an unknown publisher. Press More info, then Run anyway.</p>
      <p class="whylink"><a href="why.html">Why use an installer?</a></p>
    </section>

    <section id="discord">
      <h2>Come play</h2>
      <p>Updates, matches and bug reports all happen on the Discord.</p>
      <div class="cta" style="margin-top:0"><a class="btn primary" href="{DISCORD}" target="_blank" rel="noopener">Join the Discord</a></div>
    </section>
  </main>
""" + foot()
    open(os.path.join(out, "index.html"), "w", encoding="utf-8").write(index)

    # ---------------- why page
    why = head("Why an installer? - " + SITE_NAME,
               "Why Fight or Flight : Hide N Seek uses an installer instead of Steam Workshop addons.", CSS, "") + f"""
  <main>
    <div class="pagehead">
      <a class="backlink" href="index.html">&larr; Back</a>
      <h1>Why an installer?</h1>
      <p>Most Garry's Mod content comes from the Steam Workshop. This gamemode doesn't, and here's why.</p>
    </div>

    <section>
      <ul class="why">
        <li><b>Some addons can't go on the Workshop</b>The gamemode includes modified versions of other people's addons and content. The Workshop only accepts content you made or have permission to upload, and reuploads risk being taken down. An installer lets all of it still be shared in one place.</li>
        <li><b>One click, everything</b>No subscribing to dozens of separate addons and hoping you got them all.</li>
        <li><b>Everyone on the same version</b>Every player gets the same versions of every addon, so nothing breaks because of an older or different copy.</li>
        <li><b>Small, fast updates</b>After the first install it only downloads the addons that changed, so most updates take seconds.</li>
        <li><b>Updates the moment they're out</b>New killers and fixes reach you as soon as they're released, with no waiting on a review.</li>
        <li><b>Safe and simple</b>It only downloads from this project's GitHub release and only writes into your <code>garrysmod\\addons</code> folder. The source is on GitHub if you want to check.</li>
      </ul>
      <div class="cta" style="margin-top:28px">
        <a class="btn primary" href="{INSTALLER}">Download installer</a>
        <a class="btn" href="index.html#install">How to install</a>
      </div>
    </section>
  </main>
""" + foot()
    open(os.path.join(out, "why.html"), "w", encoding="utf-8").write(why)

    # ---------------- killer pages
    for i, n in enumerate(names):
        s = slug(n)
        d = desc[n]
        stats = "".join(
            f'<li><span>{esc(clean(x["label"]))}</span><b>{esc(clean(x["value"]))}</b></li>' for x in d.get("stats", []))
        items = []
        for it in d.get("items", []):
            m = re.match(r"^(.*?)\s*\(([^)]+)\)\s*$", clean(it["name"]))
            if m:
                nm, key = m.group(1), m.group(2)
                kc = "key passive" if key.strip().upper() == "PASSIVE" else "key"
                keyhtml = f'<span class="{kc}">{esc(key.title() if key.strip().upper() == "PASSIVE" else key)}</span>'
            else:
                nm, keyhtml = clean(it["name"]), ""
            items.append(f'<li><div class="an">{esc(nm)}{keyhtml}</div><p>{esc(clean(it["desc"]))}</p></li>')
        stars_n = int(diff.get(n, 0) or 0)
        stars = ""
        if stars_n:
            stars = ('<span class="stars" title="Difficulty %d of 5" aria-label="Difficulty %d of 5"><b>%s</b>%s<small>Difficulty</small></span>'
                     % (stars_n, stars_n, "★" * stars_n, "★" * (5 - stars_n)))
        text = info.get(n, "")
        paras = [p.strip() for p in str(text).split("\n\n") if p.strip()]
        tagline = paras[0] if paras else ""
        overview = "".join(f"<p>{esc(clean(p))}</p>" for p in paras[1:])
        prev_n = names[i - 1] if i > 0 else None
        next_n = names[i + 1] if i < len(names) - 1 else None
        pager = '<nav class="pager" aria-label="More killers">'
        pager += (f'<a href="{slug(prev_n)}.html"><small>Previous</small><b>{esc(prev_n)}</b></a>' if prev_n else "<span></span>")
        pager += (f'<a class="next" href="{slug(next_n)}.html"><small>Next</small><b>{esc(next_n)}</b></a>' if next_n else "")
        pager += "</nav>"
        page_desc = (tagline + " " if tagline else "") + f"{n} in {SITE_NAME}: stats and abilities."
        body = f"""
  <main>
    <a class="crumb" href="../index.html#killers">&larr; All killers</a>
    <article class="k">
      <div class="stage" id="stage">
        <span class="initial" aria-hidden="true">{esc(n.replace("The ", "")[:1])}</span>
        <video id="spin" autoplay loop muted playsinline preload="auto" hidden aria-label="{esc(n)} model">
          <source src="../models/{s}.webm" type="video/webm">
          <source src="../models/{s}.mp4" type="video/mp4">
        </video>
      </div>
      <div>
        <h1>{esc(n)}</h1>
        <div class="meta">{role_tag(roles.get(n))}{stars}</div>
        {f'<p class="tagline">{esc(clean(tagline))}</p>' if tagline else ""}
        <ul class="stats">{stats}</ul>
        <h2 class="abil">Abilities</h2>
        <ul class="abilities">
          {chr(10).join("          " + x for x in items).strip()}
        </ul>
        {f'<div class="over">{overview}</div>' if overview else ""}
      </div>
    </article>
    {pager}
  </main>
  <script>
    // Shows the spinning model once its video file exists; otherwise the big initial stays.
    (function () {{
      var v = document.getElementById("spin");
      if (!v) return;
      function show() {{
        v.hidden = false;
        var i = document.querySelector(".stage .initial");
        if (i) i.style.visibility = "hidden";
        var p = v.play(); if (p && p.catch) p.catch(function () {{}});
      }}
      v.addEventListener("loadeddata", show);
      if (v.readyState >= 2) show();
    }})();
  </script>
"""
        page = head(f"{n} | {SITE_NAME}", page_desc, CSS, "../") + body + foot()
        open(os.path.join(out, "killers", s + ".html"), "w", encoding="utf-8").write(page)
    print("Built %d killer pages + index" % len(names))
    return names


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    build(sys.argv[1], sys.argv[2])
