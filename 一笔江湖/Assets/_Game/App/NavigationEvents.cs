using GameFramework;
using GameFramework.Event;

namespace Yibi.App
{
    /// <summary>Pooled event data must not be retained after the event callback.</summary>
    public sealed class NavigationRequestedEventArgs : GameEventArgs
    {
        public const int EventId = 0x594201;
        public override int Id => EventId;
        public string SceneName { get; private set; }

        public static NavigationRequestedEventArgs Create(string sceneName)
        {
            var args = ReferencePool.Acquire<NavigationRequestedEventArgs>();
            args.SceneName = sceneName;
            return args;
        }

        public override void Clear() { SceneName = null; }
    }

    /// <summary>Emitted once when Unity confirms a scene has loaded.</summary>
    public sealed class NavigationCompletedEventArgs : GameEventArgs
    {
        public const int EventId = 0x594202;
        public override int Id => EventId;
        public string SceneName { get; private set; }

        public static NavigationCompletedEventArgs Create(string sceneName)
        {
            var args = ReferencePool.Acquire<NavigationCompletedEventArgs>();
            args.SceneName = sceneName;
            return args;
        }

        public override void Clear() { SceneName = null; }
    }
}
