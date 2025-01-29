using System.Reflection;
using Chaos.Time.Abstractions;

namespace ChaosAssetManager.Extensions;

public static class IntervalTimerExtensions
{
    private static readonly Func<IIntervalTimer, TimeSpan> GetElapsedFunc;
    private static readonly Action<IIntervalTimer, TimeSpan> SetElapsedAction;

    static IntervalTimerExtensions()
    {
        var property = typeof(IIntervalTimer).GetProperty("Elapsed", BindingFlags.Instance | BindingFlags.NonPublic);

        GetElapsedFunc = (Func<IIntervalTimer, TimeSpan>)Delegate.CreateDelegate(
            typeof(Func<IIntervalTimer, TimeSpan>),
            property!.GetGetMethod()!);

        SetElapsedAction = (Action<IIntervalTimer, TimeSpan>)Delegate.CreateDelegate(
            typeof(Action<IIntervalTimer, TimeSpan>),
            property.GetSetMethod()!);
    }

    /// <summary>
    ///     Uses reflection to get the elapsed property of the timer
    /// </summary>
    public static TimeSpan GetElapsed(this IIntervalTimer timer) => GetElapsedFunc(timer);

    /// <summary>
    ///     Uses reflection to set the elapsed property of the timer
    /// </summary>
    public static void SetElapsed(this IIntervalTimer timer, TimeSpan value) => SetElapsedAction(timer, value);
}