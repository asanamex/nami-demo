using Nami.Sdk;

namespace Nami.Demo.QuarantineDemo;

[NamiPlugin]
[PluginInfo("demo.nami.quarantinedemo", "QuarantineDemo", "1.0.0", Description = "Throws every Nth frame to demonstrate Nami's quarantine.")]
public sealed class QuarantineDemoPlugin : NamiPlugin
{
    private const int DefaultEveryNthFrame = 3;

    private int _ticks;

    public override void OnLoad()
    {
        int n = Math.Max(1, Context.Config.GetInt("throwEveryNthFrame", DefaultEveryNthFrame));
        Context.Log.Info($"QuarantineDemo loaded: throwing every {n} frames. The game survives; this mod gets quarantined.");
    }

    public override void OnUpdate()
    {
        _ticks++;
        int n = Math.Max(1, Context.Config.GetInt("throwEveryNthFrame", DefaultEveryNthFrame));
        if (_ticks % n == 0)
            throw new InvalidOperationException($"QuarantineDemo boom (tick {_ticks}; throws every {n} frames)");
    }
}
