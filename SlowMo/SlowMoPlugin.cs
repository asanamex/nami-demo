using Nami;
using Nami.Sdk;

namespace Nami.Demo.SlowMo;

[NamiPlugin]
[PluginInfo("demo.nami.slowmo", "SlowMo", "1.0.0", Description = "Toggles Time.timeScale on a keypress via Tide.")]
public sealed class SlowMoPlugin : NamiPlugin
{
    // Live-reload path: edit these defaults and rebuild; Nami hot-reloads the new
    // generation without restarting the game. nami.json pluginConfig edits apply
    // at the next game start (per-generation config is snapshotted at boot).
    private const double DefaultFactor = 0.3;
    private const int DefaultToggleKey = 289; // Unity KeyCode.F8 (F1=282 .. F12=293)

    private GameClass? _time;
    private GameClass? _input;
    private bool _inputWarned;
    private bool _slowed;

    public override void OnLoad()
    {
        _time = GameClass.Resolve("UnityEngine.CoreModule", "UnityEngine", "Time");
        _input = GameClass.Resolve("UnityEngine.InputLegacyModule", "UnityEngine", "Input");
        int key = Context.Config.GetInt("toggleKey", DefaultToggleKey);
        double factor = Context.Config.GetDouble("factor", DefaultFactor);
        Context.Log.Info($"SlowMo loaded: press key {key} to toggle timeScale 1.0/{factor}. Tide available: {Tide.IsAvailable}");
        // NOTE: no Tide calls here by design. At OnLoad the game is still in early
        // boot (pre-window): Unity engine state such as Time is not initialized yet,
        // and invoking the Time.timeScale getter this early AV-crashes UnityPlayer.dll
        // inside mono_runtime_invoke (native fault - uncatchable from managed code).
        // First Tide contact happens in OnUpdate once the engine is up.
    }

    public override void OnUpdate()
    {
        if (!Tide.IsAvailable || _time is null || _input is null)
            return;

        bool pressed;
        try
        {
            pressed = PollKeyDown(Context.Config.GetInt("toggleKey", DefaultToggleKey));
        }
        catch (Exception ex)
        {
            if (!_inputWarned)
            {
                _inputWarned = true;
                Context.Log.Warn($"SlowMo key poll failed (needs the legacy UnityEngine.Input class): {ex.Message}");
            }
            return;
        }
        if (!pressed)
            return;

        double factor = Context.Config.GetDouble("factor", DefaultFactor);
        _slowed = !_slowed;
        float target = _slowed ? (float)factor : 1.0f;
        try
        {
            _time.SetStaticFloat("timeScale", target);
            Context.Log.Info($"SlowMo {(_slowed ? "ON" : "OFF")}: timeScale={target}");
        }
        catch (Exception ex)
        {
            Context.Log.Error($"SlowMo timeScale write failed: {ex.Message}");
        }
    }

    private bool PollKeyDown(int keyCode)
    {
        try
        {
            var v = _input!.CallStaticValue("GetKeyDown", new[] { TideValue.FromInt(keyCode) }, TideType.Bool);
            return v.Boolean;
        }
        catch (Exception ex) when (IsNotFound(ex))
        {
            // Older Unity keeps Input in CoreModule instead of InputLegacyModule.
            // Retry there once and keep whichever resolver works.
            _input = GameClass.Resolve("UnityEngine.CoreModule", "UnityEngine", "Input");
            var v = _input.CallStaticValue("GetKeyDown", new[] { TideValue.FromInt(keyCode) }, TideType.Bool);
            return v.Boolean;
        }
    }

    private static bool IsNotFound(Exception ex) => ex is Tide.TideException te && te.Code == -1;
}
