using System;
using GameFramework;
using GameFramework.Event;
using GameFramework.Fsm;
using GameFramework.Procedure;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Yibi.App
{
    /// <summary>
    /// Saved application composition root. Owns the framework lifetime, navigation
    /// and its saved loading view; gameplay remains in its existing assemblies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameRoot : MonoBehaviour
    {
        public const string ResourcePath = "App/GameRoot";
        public static GameRoot Instance { get; private set; }

        [Header("Saved view")]
        public LoadingOverlay loadingOverlay;

        [Header("Runtime state (inspect while playing)")]
        [SerializeField] private string currentScene;
        [SerializeField] private string currentProcedure;
        [SerializeField] private string targetScene;
        [SerializeField, TextArea] private string status;
        [SerializeField, Range(0, 1)] private float progress;
        [SerializeField] private bool isLoading;
        [SerializeField] private bool hasError;
        [SerializeField] private int completedNavigations;

        private bool initialized;
        private bool navigationRequested;
        private string failedDestination;
        private Type sceneProcedureType;
        private AsyncOperation loadOperation;
        private IProcedureManager procedures;
        private IEventManager events;

        public string CurrentScene => currentScene;
        public string CurrentProcedure => currentProcedure;
        public string TargetScene => targetScene;
        public string Status => status;
        public float Progress => progress;
        public bool IsLoading => isLoading;
        public bool HasError => hasError;
        public bool CanRetry => hasError && !string.IsNullOrEmpty(failedDestination);
        public int CompletedNavigations => completedNavigations;
        public IEventManager Events => events;

        public static GameRoot Ensure()
        {
            if (Instance != null) return Instance;

            // The prefab is authored in the editor; never create a hidden UI tree.
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null || prefab.GetComponent<GameRoot>() == null)
                throw new InvalidOperationException(
                    "Missing saved GameRoot prefab at Resources/App/GameRoot.prefab. " +
                    "Create and save the application prefab before using navigation.");

            Instantiate(prefab);
            if (Instance == null || !Instance.initialized)
                throw new InvalidOperationException("GameRoot prefab did not initialize. Check its saved LoadingOverlay references.");
            return Instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Also supports entering Play with domain reload disabled.
            Instance = null;
            GameFrameworkEntry.Shutdown();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            if (loadingOverlay == null || !loadingOverlay.IsConfigured)
                throw new InvalidOperationException(
                    "GameRoot requires a saved LoadingOverlay with panel, labels, slider and retry/cancel buttons.");

            Instance = this;
            DontDestroyOnLoad(gameObject);
            currentScene = SceneManager.GetActiveScene().name;
            sceneProcedureType = ProcedureForScene(currentScene);
            loadOperation = null;
            targetScene = null;
            failedDestination = null;
            progress = 0;
            isLoading = hasError = navigationRequested = false;
            status = string.Empty;
            completedNavigations = 0;

            var fsm = GameFrameworkEntry.GetModule<IFsmManager>();
            events = GameFrameworkEntry.GetModule<IEventManager>();
            events.Subscribe(NavigationRequestedEventArgs.EventId, OnNavigationRequested);
            procedures = GameFrameworkEntry.GetModule<IProcedureManager>();
            procedures.Initialize(fsm,
                new ProcedureBoot(this), new ProcedureLoading(this), new ProcedureError(this),
                new ProcedureMainMenu(this), new ProcedureWorld(this), new ProcedurePractice(this),
                new ProcedureLobby(this), new ProcedureBattle(this), new ProcedureWorkshop(this),
                new ProcedureExternalScene(this));

            SceneManager.sceneLoaded += OnSceneLoaded;
            initialized = true;
            loadingOverlay.Bind(this);
            procedures.StartProcedure<ProcedureBoot>();
        }

        private void Update()
        {
            if (!initialized || Instance != this) return;
            if (loadOperation != null)
                progress = Mathf.Clamp01(loadOperation.progress / .9f);
            GameFrameworkEntry.Update(Time.deltaTime, Time.unscaledDeltaTime);
            loadingOverlay.Refresh();
        }

        /// <returns>True only when a new navigation request is accepted.</returns>
        public bool RequestNavigation(string sceneName)
        {
            if (!initialized) throw new InvalidOperationException("GameRoot is not initialized.");
            if (isLoading) return false;

            GameScene destination;
            if (!GameScenes.TryParse(sceneName, out destination))
            {
                FailNavigation(sceneName, "找不到目标场景，请返回当前画面后重试。");
                return false;
            }
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                FailNavigation(sceneName, "目标场景未包含在此版本中，请返回当前画面。");
                return false;
            }
            if (string.Equals(currentScene, sceneName, StringComparison.Ordinal))
            {
                DismissError();
                return false;
            }

            // FireNow is intentional: the handler acquires the input/load gate
            // before this call returns, so a second click cannot queue another load.
            events.FireNow(this, NavigationRequestedEventArgs.Create(sceneName));
            return true;
        }

        private void OnNavigationRequested(object sender, GameEventArgs args)
        {
            var request = args as NavigationRequestedEventArgs;
            if (!ReferenceEquals(sender, this) || request == null || isLoading) return;
            targetScene = request.SceneName;
            failedDestination = null;
            hasError = false;
            isLoading = navigationRequested = true;
            progress = 0;
            status = "正在前往下一处江湖…";
            loadingOverlay.Refresh();
        }

        internal void BeginPendingLoad()
        {
            if (!navigationRequested || loadOperation != null) return;
            navigationRequested = false;
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
                if (loadOperation == null)
                    FailNavigation(targetScene, "场景加载未能启动，请重试。");
            }
            catch (Exception exception)
            {
                // The user gets a recovery action; diagnostics retain the cause.
                Debug.LogWarning("[Yibi.App] Scene load failed: " + targetScene + " (" + exception.Message + ")");
                FailNavigation(targetScene, "场景加载失败，请重试或返回当前画面。");
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Additive content is not a navigation destination. Legacy direct
            // single-scene loads remain supported, including existing tests.
            if (mode != LoadSceneMode.Single) return;
            currentScene = scene.name;
            sceneProcedureType = ProcedureForScene(currentScene);
            bool expected = isLoading && string.Equals(targetScene, scene.name, StringComparison.Ordinal);
            if (expected)
            {
                loadOperation = null;
                targetScene = null;
                isLoading = navigationRequested = hasError = false;
                failedDestination = null;
                progress = 1;
                status = string.Empty;
                completedNavigations++;
            }
            else if (!isLoading)
            {
                // A direct legacy load also clears stale navigation errors.
                targetScene = null;
                failedDestination = null;
                hasError = false;
                status = string.Empty;
            }
            events.Fire(this, NavigationCompletedEventArgs.Create(scene.name));
            loadingOverlay.Refresh();
        }

        private void FailNavigation(string destination, string message)
        {
            loadOperation = null;
            navigationRequested = isLoading = false;
            hasError = true;
            failedDestination = destination;
            targetScene = destination;
            status = message;
            progress = 0;
            loadingOverlay.Refresh();
        }

        public void RetryNavigation()
        {
            if (!CanRetry || isLoading) return;
            RequestNavigation(failedDestination);
        }

        public void DismissError()
        {
            if (isLoading) return; // Unity scene activation cannot be cancelled safely.
            hasError = false;
            targetScene = failedDestination = null;
            status = string.Empty;
            loadingOverlay.Refresh();
        }

        internal void SetProcedure(string name) { currentProcedure = name; }

        internal Type DesiredProcedure()
        {
            if (hasError) return typeof(ProcedureError);
            if (isLoading) return typeof(ProcedureLoading);
            return sceneProcedureType;
        }

        // Resolve only when the scene changes, not in the per-frame FSM poll.
        // Enum.IsDefined(Type, object) would otherwise box a value every frame.
        private static Type ProcedureForScene(string sceneName)
        {
            GameScene scene;
            if (!GameScenes.TryParse(sceneName, out scene)) return typeof(ProcedureExternalScene);
            switch (scene)
            {
                case GameScene.MainMenu: return typeof(ProcedureMainMenu);
                case GameScene.Valley: return typeof(ProcedureWorld);
                case GameScene.GestureLab: return typeof(ProcedurePractice);
                case GameScene.Lobby: return typeof(ProcedureLobby);
                case GameScene.Arena_Stone:
                case GameScene.Arena_Bamboo: return typeof(ProcedureBattle);
                case GameScene.AnimationWorkshop: return typeof(ProcedureWorkshop);
                default: return typeof(ProcedureExternalScene);
            }
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (events != null)
                events.Unsubscribe(NavigationRequestedEventArgs.EventId, OnNavigationRequested);
            if (loadingOverlay != null) loadingOverlay.Unbind(this);
            initialized = false;
            GameFrameworkEntry.Shutdown();
            events = null;
            procedures = null;
            Instance = null;
        }
    }
}
