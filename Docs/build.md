# Building the Web player

## From a fresh clone to playing

```sh
git clone <this repo> && cd flappy-voice-app
npm run build                 # no npm install — build.mjs uses node builtins only
python3 Tools/serve-web.py    # then open http://localhost:8123/
```

You need **Unity 6000.6.0f1** (pinned in `ProjectSettings/ProjectVersion.txt`, which the tooling
reads — a mismatched editor is a hard failure) with the **WebGL Build Support** module, added via
Hub → Installs → the **⋯** menu on 6000.6.0f1 → Add modules. `build.mjs` finds the editor wherever
the Hub put it; `UNITY_EDITOR_PATH=/path/to/binary` overrides the search.

Output lands in `dist/` (git-ignored). `Tools/build-web.sh` and the **Flappy Voice → Build Web
Player** menu item run the same thing.

Four things that will bite you:

- **Close the Editor first** — Unity locks the project to one instance.
- **Serve with `Tools/serve-web.py`, not `python3 -m http.server`.** The payload is Brotli with
  `decompressionFallback: false`, so the server *must* send `Content-Encoding: br` or the loader
  never starts.
- **Open the localhost URL it prints, not the LAN one.** Plain HTTP is not a secure context, so
  `getUserMedia` is refused and the game hears nothing.
- **The template-drift warning on every build is expected** — the Web template is restyled on
  purpose (see `CLAUDE.md`). Never resolve it by copying the canonical file over ours.

High stripping (`ManagedStrippingLevel.High`) is on, and it breaks things at runtime that compile
fine — a green build is not a passing build, play it.

## Does it work with the parent repo?

Yes. This repo is a submodule of `variant-originals` at `games/flappy-voice/`, and also builds
standalone — `build.mjs` reimplements the parent's checks rather than importing
`../../scripts/unity/tooling.mjs`, so a clone with no parent above it still works.

| Parent requirement | Satisfied by |
|---|---|
| `package.json` with a `build` script | `package.json` → `node build.mjs` |
| `game.json`: `"engine": "unity"`, `"publishDir": "dist"` | `game.json`, re-checked before the build starts |
| Output in `dist/` | `WebBuilder.BuildWeb` |
| A pinned editor version it can read | `ProjectSettings/ProjectVersion.txt` |
| The shared `Variant` Web template | `Assets/WebGLTemplates/Variant/index.html`, restyled on purpose |
| Release settings (stripping, no splash, Brotli, no threads) | `WebBuilder.ApplyWebSettings`, mirroring `OriginalsWebBuild` |
| Under the 75 MB ceiling | enforced by `build.mjs` |
| `com.variant.originals.web` in `Packages/manifest.json` | **not satisfied, deliberately** |

That last one: the path it wants (`file:../../../scripts/unity/package`) only resolves inside a
parent checkout, so adding it would stop a standalone clone from opening. We consume nothing from
the package. At `status: "local"` nothing enforces it; settle it with whoever owns
`variant-originals` before the status moves to `dev`.

## Hosting it

The output is a static directory, safe to mount at any depth (`/originals/flappy-voice/v1/`): asset
URLs are relative, there is no service worker, and `threadsSupport: false` means no COOP/COEP
headers. What the host must do:

- **Send `Content-Encoding: br`** on the three `Build/dist.*.br` files, and not re-compress them.
- **Serve over HTTPS**, or the microphone is refused.
- **End the URL in a slash** (or name `index.html`), since the asset URLs are relative.

Nice-to-have: `Content-Type: application/wasm` on `dist.wasm.br`, for streaming compile.
