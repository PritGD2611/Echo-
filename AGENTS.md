# Echo — agent notes

## Layout

- `Echo/` — the Unity project (**6000.3.8f1**)
- `Echo/Assets/Script/` — gameplay scripts (`Camera/`, `Journal/`)
- `Echo/Assets/Editor/EchoCli.cs` — batch entry points (`Info`, `Ping`, `Validate`, `ValidateLive`, `Refresh`, `Build`)
- `Echo/Assets/Editor/EchoCliBridge.cs` — file-queue bridge that runs commands inside the open Editor
- `tools/unity-cli.cmd` / `tools/unity-cli.ps1` — CLI entry point

## Driving Unity

Never close the user's Editor to run a command. `tools/unity-cli.cmd` routes
automatically:

| State | Where the command runs |
|---|---|
| Editor closed | batch mode on the real project |
| Editor open, idle | inside the open Editor via `Temp/echo-cli/queue` (ping/info/compile/validate/exec) |
| Editor busy or Play Mode | shadow copy at `D:\Unity\.echo-cli-shadow\Echo` |

`test` and `build` always use the shadow copy while the Editor is open, so a
multi-minute build never blocks or disturbs the Editor.

```powershell
.\tools\unity-cli.cmd ping                 # live Editor health
.\tools\unity-cli.cmd info                 # project + build settings
.\tools\unity-cli.cmd compile              # compile check
.\tools\unity-cli.cmd validate             # compile + missing script/scene checks
.\tools\unity-cli.cmd exec EchoCli.Info    # any static Editor method
.\tools\unity-cli.cmd test edit            # EditMode tests
.\tools\unity-cli.cmd test play            # PlayMode tests
.\tools\unity-cli.cmd build win            # Builds/Windows/Echo.exe
.\tools\unity-cli.cmd build android        # Builds/Android/Echo.apk
.\tools\unity-cli.cmd refresh              # force reimport in the live Editor
.\tools\unity-cli.cmd logs 80              # tail newest CLI log
.\tools\unity-cli.cmd shadow reset         # delete the shadow copy
```

Flags: `-Shadow` (force the copy), `-StopEditor` (close the Editor instead),
`-Yes`, `-NoGraphics`, `-ProjectPath <dir>`. Override the shadow location with
`ECHO_CLI_SHADOW`.

Behavior:

- Resolves Unity from `ProjectSettings/ProjectVersion.txt`; override with `$env:UNITY_EDITOR`.
- Logs go to `Echo/Logs/cli/<timestamp>-<command>[-shadow].log` (git-ignored).
- Fails on `error CS...`, compiler errors, `[EchoCli] PROBLEM`, or `[EchoCli] EXCEPTION` even when Unity exits 0.
- Shadow syncs `Assets`, `Packages`, `ProjectSettings` with robocopy before each run; the first run also copies `Library` (~2 GB).
- Build output is written to the real project's `Builds/`, not the shadow.

### Adding commands

Add a `public static void` method to `EchoCli`. Use it with `exec`; to make it
available inside the live Editor, add the method name to `EchoCliBridge.AllowedMethods`
and to `$script:LiveAllowedMethods` in `tools/unity-cli.ps1`. Methods must call
`EditorApplication.Exit(code)` in batch mode for a meaningful exit status, and
must not open/close scenes if they are live-safe.

Live-safe means: read-only, no scene loading, no domain reload, no modal dialogs.

## Style

Existing scripts use no namespaces, `MonoBehaviour` with `[SerializeField]`
private fields, and simple `Start`/`Update` logic. Match that in gameplay code.
