using Nami;
using Nami.Sdk;

namespace Nami.Demo.FpsOverlay;

[NamiPlugin]
[PluginInfo("demo.nami.fpsoverlay", "FpsOverlay", "1.0.0", Description = "Throttled FPS + camera/position readout via Tide.")]
public sealed class FpsOverlayPlugin : NamiPlugin
{
    private const double DefaultIntervalSeconds = 5.0;

    private int _ticks;
    private int _frames;
    private DateTime _windowStart = DateTime.UtcNow;

    public override void OnLoad()
    {
        _windowStart = DateTime.UtcNow;
        double interval = Context.Config.GetDouble("intervalSeconds", DefaultIntervalSeconds);
        Context.Log.Info($"FpsOverlay loaded: logging every {interval}s. Tide available: {Tide.IsAvailable}");
    }

    public override void OnUpdate()
    {
        _ticks++;
        _frames++;
        var now = DateTime.UtcNow;
        double interval = Math.Max(1.0, Context.Config.GetDouble("intervalSeconds", DefaultIntervalSeconds));
        if ((now - _windowStart).TotalSeconds < interval)
            return;

        double fps = _frames / (now - _windowStart).TotalSeconds;
        _frames = 0;
        _windowStart = now;

        string line = Tide.IsAvailable
            ? $"FpsOverlay: {fps:F0} fps | {CameraSummary()} (tick {_ticks})"
            : $"FpsOverlay: {fps:F0} fps | no Tide, game values unavailable (tick {_ticks})";
        Context.Log.Info(line);
        if (Tide.IsAvailable)
        {
            try
            {
                Tide.UnityLog(line);
            }
            catch (Exception ex)
            {
                Context.Log.Warn($"FpsOverlay Unity console mirror failed: {ex.Message}");
            }
        }
    }

    // NEVER read Camera.main (or any singular FindObjectOfType wrapper) through Tide:
    // Unity aborts the process (0xe0000001) when it runs outside managed game code.
    // GameClass.FindObject() uses FindObjectsOfType + element 0, which returns cleanly.
    private static string CameraSummary()
    {
        try
        {
            var cameraClass = GameClass.Resolve("UnityEngine.CoreModule", "UnityEngine", "Camera");
            using var camera = cameraClass.FindObject();
            if (camera is null)
                return "camera=(none)";
            string name = camera.GetString("name") ?? "(unnamed)";
            return $"camera='{name}' pos={TryPosition(camera)}";
        }
        catch (Exception ex)
        {
            return $"camera=(error: {ex.Message})";
        }
    }

    private static string TryPosition(GameObject camera)
    {
        try
        {
            using var transform = camera.GetObject("transform");
            if (transform is null)
                return "n/a";
            using var position = transform.GetObject("position");
            if (position is null)
                return "n/a";
            float x = position.GetFloat("x");
            float y = position.GetFloat("y");
            float z = position.GetFloat("z");
            return $"{x:F1},{y:F1},{z:F1}";
        }
        catch
        {
            // Value-type (Vector3) field reads are best-effort: not every game
            // exposes them through Tide, so fall back instead of failing.
            return "n/a";
        }
    }
}
