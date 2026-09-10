using System.Reflection;
using Nami.Sdk;

namespace Nami.Demo.SkipSplash;

/// <summary>
/// Stand-in for a real splash screen. Wave patches Nami's own .NET runtime (never game
/// Mono), so this demo ships its own patchable target: point className/methodName at a
/// real CoreCLR-visible method to skip that instead (see README).
/// </summary>
public static class FakeSplash
{
    private static int _shown;
    private static int _shownTick;

    public static int Shown => Volatile.Read(ref _shown);

    public static void Show()
    {
        Interlocked.Increment(ref _shown);
        Volatile.Write(ref _shownTick, Environment.TickCount);
    }
}

[NamiPlugin]
[PluginInfo("demo.nami.skipsplash", "SkipSplash", "1.0.0", Description = "Config-driven Wave prefix-skip demo on a stand-in splash method.")]
public sealed class SkipSplashPlugin : NamiPlugin
{
    private const string DefaultClassName = "Nami.Demo.SkipSplash.FakeSplash";
    private const string DefaultMethodName = "Show";
    private const string SlotKey = "skip-splash";
    private const int DefaultDemoIntervalTicks = 300;

    // Written by the Wave prefix (may run on any thread); read on the tick thread.
    private static int s_skipped;

    private int _ticks;

    public override void OnLoad()
    {
        string className = Context.Config.GetString("className", DefaultClassName);
        string methodName = Context.Config.GetString("methodName", DefaultMethodName);
        bool skip = Context.Config.GetBool("skip", true);
        try
        {
            var target = ResolveTarget(className, methodName);
            if (target is null)
            {
                Context.Log.Error($"SkipSplash: target {className}.{methodName} not found; patch not registered.");
                return;
            }

            Func<bool> prefix = () =>
            {
                if (Context.Config.GetBool("skip", true))
                {
                    Interlocked.Increment(ref s_skipped);
                    return false; // skip the original body (Wave prefix convention)
                }
                return true;
            };
            Context.Hooks.PatchPrefix(target, SlotKey, prefix);
            Context.Log.Info($"SkipSplash loaded: prefix on {target.DeclaringType?.FullName}.{target.Name} (skip={skip})");
        }
        catch (Exception ex)
        {
            Context.Log.Error($"SkipSplash patch failed: {ex.Message}");
        }
    }

    public override void OnUpdate()
    {
        int interval = Math.Max(1, Context.Config.GetInt("demoIntervalTicks", DefaultDemoIntervalTicks));
        if (++_ticks % interval != 0)
            return;

        int before = Volatile.Read(ref s_skipped);
        FakeSplash.Show();
        int after = Volatile.Read(ref s_skipped);
        if (after > before)
            Context.Log.Info($"SkipSplash: splash skipped by prefix (total skips={after}, plays={FakeSplash.Shown})");
        else
            Context.Log.Info($"SkipSplash: splash played (plays={FakeSplash.Shown})");
    }

    private static MethodBase? ResolveTarget(string className, string methodName)
    {
        var type = typeof(SkipSplashPlugin).Assembly.GetType(className)
            ?? Type.GetType(className, throwOnError: false);
        return type?.GetMethod(methodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
    }
}
