# itch.io publish (#221 / #83)

Desktop Windows channel already exists as a GitHub Release
([v0.3.0-alpha.3](https://github.com/aawadall/strategos/releases/tag/v0.3.0-alpha.3)).
Pages download CTA is live on [aawadall.github.io/strategos](https://aawadall.github.io/strategos/).
**Live itch page:** [aawadall.itch.io/strategos](https://aawadall.itch.io/strategos)
(`user/game` = `aawadall/strategos`).

WebGL playable embed is **#220** — not required for a Windows itch channel.

---

## Do you need butler?

| Path | Needs separate butler? |
|---|---|
| Browser upload on Edit game | No |
| itch desktop app Upload / Builds (v26.12.0+) | No — bundled butler + your login |
| CLI `butler push` / `scripts/itch-push.ps1` | Yes — install butler **or** use the app-bundled binary |

itch **account login alone** is not a push tool. For a first alpha zip (~38 MB), browser
or the app Upload tab is enough. Install standalone butler only for scripts / CI.

Install notes: [butler installing](https://itchio.github.io/butler/installing.html).
App GUI: [pushing builds in the itch app](https://itch.io/updates/pushing-builds-with-butler-is-now-in-the-itch-app).

---

## One-time project setup

1. Log into itch.io → **Create new project**.
2. Project URL: [aawadall.itch.io/strategos](https://aawadall.itch.io/strategos)
   (`STRATEGOS_ITCH_TARGET=aawadall/strategos` for `itch-push.ps1`).
3. Classification: **Game**. Kind: downloadable (Windows). Price: **pay-what-you-want**
   with a **$2** suggested default (live on aawadall.itch.io/strategos). GitHub Release zip
   stays a free mirror.
4. Short description: reuse README / site blurb; link GitHub Release notes and alpha limits
   (fog / artillery DF / no ZoC).
5. After first upload, set channel tags if needed (Windows executable). HTML5 channel waits
   on #220.

---

## What to upload

Prefer the **Release asset**, not a random local `Artifacts` folder:

- Zip: `Strategos-0.3.0-alpha.3-windows.zip` from the GitHub Release, **or**
- Unzipped folder that contains `Strategos.exe` (butler can push a directory).

Channel name convention: `windows` (stable alpha) or `windows-alpha` for the first push.

---

## Push options

### A — Browser

Edit game → Uploads → upload the zip → mark as Windows → Save.

### B — itch app

Open the app (logged in) → **Upload** / Builds → pick project + channel → drop folder or zip.

### C — CLI / script

```powershell
# once
butler login

# from repo root — zip path or unzipped Windows build dir
.\scripts\itch-push.ps1 -Source path\to\Strategos-0.3.0-alpha.3-windows.zip
# or
$env:STRATEGOS_ITCH_TARGET = 'aawadall/strategos'
.\scripts\itch-push.ps1 -Source .\Artifacts\Windows -Channel windows-alpha
```

`itch-push.ps1` looks for `butler` on `PATH`, then the itch-app broth install under
`%APPDATA%\itch\broth\butler\versions\...\butler.exe`.

---

## Human checklist (#221)

- [x] Create itch project; note `user/game` → `aawadall/strategos`.
- [x] Upload Windows Release zip (browser, app, or script).
- [ ] Smoke-download on a clean machine / second account: unzip → `Strategos.exe` boots.
- [x] Add itch URL to [README.md](../README.md) Play line.
- [x] Mirror the link on the Pages site header CTA.
- [x] itch embed widget on Pages (`site/index.html`); README uses badges (GitHub strips iframes).

Closing #221 when this page + `itch-push.ps1` ship is the **ops handoff** (same shape as
#216). The first live push is a human step with your itch credentials.

---

## Marketing checklist (#480)

Storefront polish on [aawadall.itch.io/strategos](https://aawadall.itch.io/strategos) —
distinct from butler push (#221) and from Steam assets (#293).

**In-repo pack (ready to paste/upload):** [itch-page-copy.md](itch-page-copy.md) +
`Research/store/itch/`.

- [x] Cover image + 3 gameplay screenshots staged under `Research/store/itch/` (menu, PLAY, EXPLORE).
- [x] Short + long page copy drafted in `docs/itch-page-copy.md` (how-to, alpha limits, Pages / GitHub links).
- [x] Tags / genre / Windows / PWYW **$2** listed in `itch-page-copy.md` (confirm on Edit game).
- [x] First itch **devlog** draft in `itch-page-copy.md` (publish on itch to finish).
- [ ] **Operator:** paste copy, upload images, publish devlog on the live page → close #480.
- [ ] Optional: motion capture if Pages GIF (#210) is ready to reuse.

---

## Discoverability checklist (#496)

itch.io post-publish onboarding — stand out, get indexed, share. Parent epic
[#496](https://github.com/aawadall/strategos/issues/496). Complements #480 (assets/copy);
does not replace Steam trailer work (#327).

### Make the page stand out

- [ ] Trailer or gameplay video in the theme editor — [#497](https://github.com/aawadall/strategos/issues/497)
- [ ] Customize theme (colors, fonts, images) — [#498](https://github.com/aawadall/strategos/issues/498)
- [ ] Review [quality guidelines](https://itch.io/docs/creators/quality-guidelines); close page gaps — [#499](https://github.com/aawadall/strategos/issues/499)

### Get noticed / indexed

- [ ] Review [getting indexed](https://itch.io/docs/creators/getting-indexed); complete metadata — [#500](https://github.com/aawadall/strategos/issues/500)

### Community & social

- [ ] Post in the itch [community](https://itch.io/community) — [#501](https://github.com/aawadall/strategos/issues/501)
- [ ] Twitter/X with image or GIF; may mention [@itchio](https://twitter.com/itchio) — [#502](https://github.com/aawadall/strategos/issues/502)
- [ ] Reddit [r/itchio](https://www.reddit.com/r/itchio/) — [#503](https://github.com/aawadall/strategos/issues/503)
- [ ] Facebook tagged [@itchiogames](https://www.facebook.com/itchiogames) — [#504](https://github.com/aawadall/strategos/issues/504)

---

## Cross-links

- Release zip / version stamp: [build-and-verify.md](build-and-verify.md), #217–#219
- Pages CTA: #211 / #437 (done)
- WebGL verify: #220
- itch marketing: #480
- itch discoverability: #496
- Release epic: #83
