using System;

namespace Yibi.App
{
    /// <summary>Supported destinations; enum names match saved scene names.</summary>
    public enum GameScene
    {
        MainMenu,
        Valley,
        GestureLab,
        Lobby,
        Arena_Stone,
        Arena_Bamboo,
        AnimationWorkshop
    }

    public static class GameScenes
    {
        public static bool TryParse(string name, out GameScene scene)
        {
            // Enum.TryParse alone also accepts integers, which are not scene names.
            return Enum.TryParse(name, false, out scene)
                && Enum.IsDefined(typeof(GameScene), scene)
                && string.Equals(scene.ToString(), name, StringComparison.Ordinal);
        }
    }
}
