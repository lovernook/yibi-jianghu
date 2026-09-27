namespace Yibi.App
{
    /// <summary>The only entry point presenters need for changing scenes.</summary>
    public static class GameNavigation
    {
        public static bool IsInputBlocked => GameRoot.Instance != null
            && (GameRoot.Instance.IsLoading || GameRoot.Instance.HasError);

        public static bool GoTo(GameScene scene)
        {
            return GoTo(scene.ToString());
        }

        public static bool GoTo(string sceneName)
        {
            return GameRoot.Ensure().RequestNavigation(sceneName);
        }
    }
}
