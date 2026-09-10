# nami-demo

Four tiny user-facing demo mods for Nami 1.0.2. Each mod is one `.cs` file plus its
csproj. All four build standalone from a zip download: `lib/` holds the prebuilt
`Nami.Sdk.dll` / `Nami.Tide.dll` (no git submodules, no NuGet yet — see
`lib/README.md`).

| Mod | Id | Needs Tide | What it does |
|---|---|---|---|
| SlowMo | `demo.nami.slowmo` | yes | Keypress toggles `Time.timeScale` 1.0 / factor |
| FpsOverlay | `demo.nami.fpsoverlay` | yes (FPS works without) | Throttled FPS + camera/position readout |
| SkipSplash | `demo.nami.skipsplash` | no | Wave prefix-skip on a config-driven target |
| QuarantineDemo | `demo.nami.quarantinedemo` | no | Throws every Nth frame to show quarantine |

Game-agnostic except where documented below. Tide calls run on Mono and IL2CPP
(auto-detected backend).

## Prerequisites

- Windows 10/11 x64, a Unity game (Mono or IL2CPP).
- Nami 1.0.2 installed: take the `nami-1.0.2.zip` release file, extract it next to
  the release's `InstallNami.exe`, run `InstallNami.exe` inside the game folder.
  This creates `<game>/nami/` (`mods/`, `nami.json`, `nami.log`).
- .NET SDK 10.0+ to build the mods.
- For SlowMo and FpsOverlay (Tide mods): `"enableMonoBridge": true` in
  `<game>/nami/nami.json`.

## Run

From this folder, one mod at a time (replace the game path):

```bat
nami run "SlowMo\SlowMo.csproj" "C:\Games\YourGame"
nami run "FpsOverlay\FpsOverlay.csproj" "C:\Games\YourGame"
nami run "SkipSplash\SkipSplash.csproj" "C:\Games\YourGame"
nami run "QuarantineDemo\QuarantineDemo.csproj" "C:\Games\YourGame"
```

`nami run` builds the mod, stages the DLL into `<game>/nami/mods/`, and launches
the game with Nami. Watch `<game>/nami/nami.log` while the game runs.

Optional per-mod config in `<game>/nami/nami.json` (all keys optional; missing
keys use the defaults shown):

```json
{
  "enableMonoBridge": true,
  "pluginConfig": {
    "demo.nami.slowmo": { "toggleKey": 289, "factor": 0.3 },
    "demo.nami.fpsoverlay": { "intervalSeconds": 5.0 },
    "demo.nami.skipsplash": { "skip": true, "className": "Nami.Demo.SkipSplash.FakeSplash", "methodName": "Show", "demoIntervalTicks": 300 },
    "demo.nami.quarantinedemo": { "throwEveryNthFrame": 3 }
  }
}
```

Config sections are snapshotted at boot: editing `nami.json` applies at the next
game start. Editing a mod's source and rebuilding applies via hot reload with no
restart (see SlowMo below).

## SlowMo — the 30-second demo

Press F8 in-game to toggle slow motion. `toggleKey` is a Unity `KeyCode` int
(F8 = 289, F1 = 282 … F12 = 293). `factor` is the slowed `timeScale`.

Input path: `UnityEngine.Input.GetKeyDown(KeyCode)` polled once per tick through
Tide (`UnityEngine.InputLegacyModule`, falling back to `UnityEngine.CoreModule`
on older Unity). One Tide round trip per tick; failures log once and input polling
stops erroring silently afterwards. Games using only the new Input System package
may not answer legacy `Input` — the mod then logs the Tide error and does nothing.

Success looks like (in `nami.log`):

```
[demo.nami.slowmo] SlowMo loaded: press key 289 to toggle timeScale 1.0/0.3. Tide available: True
[demo.nami.slowmo] SlowMo current timeScale=1
[demo.nami.slowmo] SlowMo ON: timeScale=0.3
[demo.nami.slowmo] SlowMo OFF: timeScale=1
```

Live-reload the factor: change `DefaultFactor` in `SlowMo/SlowMoPlugin.cs`
(e.g. `0.3` → `0.1`), rebuild (`dotnet build SlowMo/SlowMo.csproj -c Release`)
and copy `SlowMo/bin/Release/net10.0/SlowMo.dll` over `<game>/nami/mods/SlowMo.dll`.
Nami hot-reloads without restarting the game (`Reloaded 'demo.nami.slowmo' ...`
in the log); the next F8 press uses the new factor.

## FpsOverlay — FPS + position readout

Logs one line per `intervalSeconds` (default 5): mod-tick FPS (equals game FPS;
`OnUpdate` runs per frame), `Camera.main` name, and camera position.

Overlay path: there is no universal on-screen text without game-specific UI
code, and mods reference only `Nami.Sdk`/`Nami.Tide` (no `UnityEngine.dll`), so
this demo uses the readout that works in every Unity game: a throttled
`nami.log` line plus a throttled mirror to the Unity console via
`Tide.UnityLog`. A true on-screen overlay needs a game-specific path (a
`UnityEngine.UI.Text` or `OnGUI`/`GUI.Label` component created for that game).

Position is best-effort: `Camera.main` → `transform` → `position` → `x/y/z`.
`Vector3` is a value type and not every game exposes its fields through Tide; on
failure the mod prints `pos=n/a` and keeps reporting FPS + camera name. Camera
name (`Camera.main` + `name`) uses the same calls as Nami's own TideProbe sample
and works wherever Tide works.

Success looks like:

```
[demo.nami.fpsoverlay] FpsOverlay loaded: logging every 5s. Tide available: True
[demo.nami.fpsoverlay] FpsOverlay: 60 fps | camera='MainCamera' pos=12.3,4.5,6.7 (tick 301)
```

Without Tide (`enableMonoBridge` off / outside a game): `FpsOverlay: 60 fps |
no Tide, game values unavailable (tick 301)`.

## SkipSplash — Wave prefix-skip

Registers a Wave prefix (`Context.Hooks.PatchPrefix`, slot `skip-splash`) that
returns `false` to skip the target method body while `skip` is true. The default
target is the in-mod stand-in `Nami.Demo.SkipSplash.FakeSplash.Show`, driven
every `demoIntervalTicks` so the skip is observable in any game with no game
code involved.

Wave patches Nami's own .NET runtime, never game Mono. To point the demo at a
real method: set `className`/`methodName` to a CoreCLR-visible static method
with a compatible shape (parameterless `Func<bool>` prefix fits parameterless
targets), rebuild to trigger hot reload — the new generation re-registers the
slot. Wave refuses methods it cannot detour safely (tiny/indecodable prologues)
with a `HookException`, which the mod logs instead of crashing.

Success looks like (defaults):

```
[demo.nami.skipsplash] SkipSplash loaded: prefix on Nami.Demo.SkipSplash.FakeSplash.Show (skip=True)
[demo.nami.skipsplash] SkipSplash: splash skipped by prefix (total skips=1, plays=0)
```

With `"skip": false` (after restart): `SkipSplash: splash played (plays=1)`.

## QuarantineDemo — quarantine proof

Throws `InvalidOperationException` every `throwEveryNthFrame`th frame (default
3). Default Nami config quarantines after 5 consecutive `OnUpdate` throws, so
this mod is disabled ~15 frames after load while the game keeps running.

Success looks like:

```
[demo.nami.quarantinedemo] QuarantineDemo loaded: throwing every 3 frames. ...
[demo.nami.quarantinedemo] OnUpdate threw (1 consecutive): System.InvalidOperationException: QuarantineDemo boom ...
...
[demo.nami.quarantinedemo] OnUpdate threw (5 consecutive): System.InvalidOperationException: QuarantineDemo boom ...
[chainloader] Quarantining plugin 'demo.nami.quarantinedemo' after 5 consecutive failures: QuarantineDemo boom ...
```

Remove the DLL from `<game>/nami/mods/` (or exclude its id via `enabledPlugins`)
after watching it happen; there is nothing else to configure.

## Troubleshooting

- `nami/nami.log` answers almost everything: boot block, per-mod load lines,
  hot-reload lines (`Reloaded '...' gen N -> gen M`), Tide errors, quarantine lines.
- Game-call failures add detail in `nami/native/nami-tide.log`.
- `nami status <gameDir>` renders the live runtime (`no live runtime` + exit 1
  when the game is not running).
- `nami doctor <gameDir>` sanity-checks an install (root, mods, exe, payload state).
- Tide mods log `Tide available: False` outside a game or without
  `"enableMonoBridge": true` — SlowMo then idles, FpsOverlay reports FPS only.
