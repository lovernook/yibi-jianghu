using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GameFramework;
using GameFramework.Event;
using GameFramework.Procedure;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yibi.App;
using Yibi.Battle;
using Yibi.Networking;

namespace Yibi.Diagnostics
{
    // Opt-in native player verification; never creates or modifies the player's save.
    [UnityEngine.Scripting.Preserve]
    public sealed class AppFlowRun : MonoBehaviour
    {
        [Serializable] public sealed class Step
        {
            public string name, scene, procedure;
            public int roots, completedNavigations;
            public bool loading, error, inputBlocked;
            public double elapsedSeconds;
        }

        [Serializable] public sealed class CapturedLog
        {
            public string type, message, stackTrace;
        }

        [Serializable] public sealed class Report
        {
            public string utc, unity, platform, initialScene, failure;
            public bool passed, finished;
            public int acceptedNavigations, requestedEvents, completedEvents, errorLogCount;
            public double elapsedSeconds;
            public Step[] steps;
            public CapturedLog[] errors;
            public string note = "Native player, scripted scene navigation and saved UI recovery. " +
                "Uses a transient profile. Does not certify manual visual quality or multiplayer behavior.";
        }

        private const double StepTimeout = 30;
        private const double RunTimeout = 240;
        private readonly List<Step> steps = new List<Step>();
        private readonly List<CapturedLog> errors = new List<CapturedLog>();
        private readonly object logGate = new object();
        private readonly Report report = new Report();
        private string output;
        private double started;
        private bool finished;
        private IDisposable profile;
        private GameRoot root;
        private IEventManager events;
        private EventHandler<GameEventArgs> requestHandler, completeHandler;
        private int requestedEvents, completedEvents, errorLogCount;

        [UnityEngine.Scripting.Preserve]
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--app-smoke-report");
            if (index < 0) return;
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                Debug.LogError("YIBI_APP_SMOKE_FAIL --app-smoke-report requires an output path.");
                Application.Quit(2);
                return;
            }

            var evidence = new GameObject("AppFlowEvidence");
            DontDestroyOnLoad(evidence);
            var runner = evidence.AddComponent<AppFlowRun>();
            runner.output = args[index + 1];
            runner.started = Time.realtimeSinceStartupAsDouble;
            Application.logMessageReceivedThreaded += runner.CaptureLog;
            // Install before the first scene's presenters load or write progress.
            runner.profile = ProfileStore.UseTransientProfile(new PlayerProfile());
            Application.runInBackground = true;
            Debug.Log("YIBI_APP_SMOKE_BOOT");
        }

        private IEnumerator Start()
        {
            report.utc = DateTime.UtcNow.ToString("O");
            report.unity = Application.unityVersion;
            report.platform = Application.platform.ToString();
            report.initialScene = SceneManager.GetActiveScene().name;
            // Flatten nested routines so assertions and timeout exceptions are all
            // reported, rather than silently terminating a Unity child coroutine.
            var routines = new Stack<IEnumerator>();
            routines.Push(Run());
            while (routines.Count > 0 && !finished)
            {
                object current = null;
                bool moved;
                try
                {
                    moved = routines.Peek().MoveNext();
                    if (moved) current = routines.Peek().Current;
                }
                catch (Exception exception)
                {
                    Finish(false, exception.ToString());
                    yield break;
                }

                if (!moved)
                {
                    (routines.Pop() as IDisposable)?.Dispose();
                    continue;
                }
                var nested = current as IEnumerator;
                if (nested != null)
                {
                    routines.Push(nested);
                    continue;
                }
                yield return current;
            }
        }

        private IEnumerator Run()
        {
            if (SceneManager.GetActiveScene().name != "MainMenu")
            {
                var operation = SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
                Require(operation != null, "Could not start the saved main menu.");
                yield return WaitFor(() => operation.isDone, "MainMenu startup");
            }
            yield return WaitFor(() => GameRoot.Instance != null
                && GameRoot.Instance.CurrentProcedure == "ProcedureMainMenu", "saved MainMenu root");
            root = GameRoot.Instance;
            Require(root.loadingOverlay != null && root.loadingOverlay.IsConfigured,
                "Saved GameRoot loading view is incomplete.");
            yield return Settle();
            Require(root.CompletedNavigations == 0, "Fresh native run must begin with no completed navigations.");
            CheckScene("MainMenu", "ProcedureMainMenu", 0);
            Record("saved-main-menu");

            events = root.Events;
            requestHandler = (sender, args) =>
            {
                Require(ReferenceEquals(sender, root), "Unexpected request event sender.");
                Require(args is NavigationRequestedEventArgs, "Unexpected request event type.");
                requestedEvents++;
            };
            completeHandler = (sender, args) =>
            {
                Require(ReferenceEquals(sender, root), "Unexpected completion event sender.");
                Require(args is NavigationCompletedEventArgs, "Unexpected completion event type.");
                completedEvents++;
            };
            events.Subscribe(NavigationRequestedEventArgs.EventId, requestHandler);
            events.Subscribe(NavigationCompletedEventArgs.EventId, completeHandler);

            Require(!GameNavigation.GoTo("InvalidSmokeDestination"), "An invalid destination was accepted.");
            Require(root.HasError && !root.IsLoading && GameNavigation.IsInputBlocked,
                "Invalid navigation must expose a recoverable error and block underlying input.");
            Require(root.loadingOverlay.panel.activeInHierarchy
                && root.loadingOverlay.cancelButton.isActiveAndEnabled
                && root.loadingOverlay.cancelButton.interactable
                && !string.IsNullOrEmpty(root.loadingOverlay.status.text), "Saved error view was not shown.");
            yield return WaitFor(() => root.CurrentProcedure == "ProcedureError", "error procedure");
            CheckActualProcedure("ProcedureError");
            Record("invalid-destination");
            root.loadingOverlay.cancelButton.onClick.Invoke();
            Require(!root.HasError && !GameNavigation.IsInputBlocked
                && !root.loadingOverlay.panel.activeSelf, "Saved cancel action did not restore input.");
            yield return WaitFor(() => root.CurrentProcedure == "ProcedureMainMenu", "dismiss error");
            CheckScene("MainMenu", "ProcedureMainMenu", 0);
            Record("error-dismissed");

            var route = new[] { GameScene.Valley, GameScene.GestureLab, GameScene.MainMenu,
                GameScene.Lobby, GameScene.MainMenu };
            var procedures = new[] { "ProcedureWorld", "ProcedurePractice", "ProcedureMainMenu",
                "ProcedureLobby", "ProcedureMainMenu" };
            for (int index = 0; index < route.Length; index++)
            {
                string target = route[index].ToString();
                string procedure = procedures[index];
                int expectedCount = index + 1;
                Require(GameNavigation.GoTo(route[index]), "Navigation rejected: " + target);
                Require(root.IsLoading && GameNavigation.IsInputBlocked
                    && root.loadingOverlay.panel.activeInHierarchy, "Loading gate missing: " + target);
                Require(!GameNavigation.GoTo(route[index] == GameScene.Valley ? GameScene.GestureLab : GameScene.Valley),
                    "A same-frame second request was accepted.");
                Require(root.TargetScene == target, "Second click replaced the accepted destination.");
                yield return WaitFor(() => !root.IsLoading && root.CurrentProcedure == procedure
                    && SceneManager.GetActiveScene().name == target, "navigation to " + target);
                yield return Settle();
                CheckScene(target, procedure, expectedCount);
                Require(requestedEvents == expectedCount && completedEvents == expectedCount,
                    "Navigation event was missing or dispatched twice.");
                Record("arrived-" + target);
            }
            Require(errorLogCount == 0, "Unity emitted errors during the native scene journey.");
            Finish(true, null);
        }

        private void CheckScene(string scene, string procedure, int completed)
        {
            Require(GameRoot.Instance == root, "Application root identity changed.");
            Require(FindObjectsOfType<GameRoot>(true).Length == 1, "Multiple application roots survived.");
            Require(SceneManager.GetActiveScene().name == scene && root.CurrentScene == scene,
                "Unity scene and application scene disagree.");
            Require(root.CompletedNavigations == completed, "Completed navigation count changed unexpectedly.");
            Require(!root.IsLoading && !root.HasError && !GameNavigation.IsInputBlocked
                && !root.loadingOverlay.panel.activeSelf, "Navigation left a stale loading/error overlay.");
            CheckActualProcedure(procedure);
        }

        private static void CheckActualProcedure(string expected)
        {
            var actual = GameFrameworkEntry.GetModule<IProcedureManager>().CurrentProcedure;
            Require(actual != null && actual.GetType().Name == expected, "Actual GF procedure is not " + expected + ".");
        }

        private void Record(string name)
        {
            steps.Add(new Step
            {
                name = name, scene = root.CurrentScene, procedure = root.CurrentProcedure,
                roots = FindObjectsOfType<GameRoot>(true).Length,
                completedNavigations = root.CompletedNavigations,
                loading = root.IsLoading, error = root.HasError, inputBlocked = GameNavigation.IsInputBlocked,
                elapsedSeconds = Time.realtimeSinceStartupAsDouble - started
            });
            WriteReport();
            Debug.Log("YIBI_APP_SMOKE_STEP " + name);
        }

        private static IEnumerator Settle()
        {
            // Let scene-start callbacks, duplicate-root destruction and the queued
            // GF completion event run before asserting stable counts.
            for (int index = 0; index < 3; index++) yield return null;
        }

        private static IEnumerator WaitFor(Func<bool> condition, string label)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + StepTimeout;
            while (!condition())
            {
                Require(Time.realtimeSinceStartupAsDouble < deadline, "Timed out waiting for " + label + ".");
                yield return null;
            }
        }

        private void Update()
        {
            if (!finished && Time.realtimeSinceStartupAsDouble - started > RunTimeout)
                Finish(false, "Overall application flow timeout.");
        }

        private void CaptureLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            lock (logGate)
            {
                errorLogCount++;
                if (errors.Count < 256)
                    errors.Add(new CapturedLog { type = type.ToString(), message = message, stackTrace = stackTrace });
            }
        }

        private void WriteReport()
        {
            report.finished = finished;
            report.elapsedSeconds = Time.realtimeSinceStartupAsDouble - started;
            report.acceptedNavigations = root == null ? 0 : root.CompletedNavigations;
            report.requestedEvents = requestedEvents;
            report.completedEvents = completedEvents;
            report.steps = steps.ToArray();
            lock (logGate)
            {
                report.errorLogCount = errorLogCount;
                report.errors = errors.ToArray();
            }
            string path = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }

        private void Finish(bool passed, string failure)
        {
            if (finished) return;
            finished = true;
            report.passed = passed;
            report.failure = failure;
            Unsubscribe();
            profile?.Dispose();
            profile = null;
            NetSession.Instance?.Disconnect();
            if (!passed) Debug.LogError("YIBI_APP_SMOKE_FAIL " + failure);
            try { WriteReport(); }
            catch (Exception exception)
            {
                passed = false;
                Debug.LogError("YIBI_APP_SMOKE_REPORT_WRITE_FAIL " + exception);
            }
            Application.logMessageReceivedThreaded -= CaptureLog;
            if (passed) Debug.Log("YIBI_APP_SMOKE_PASS");
            Application.Quit(passed ? 0 : 1);
        }

        private void Unsubscribe()
        {
            if (events == null || root == null || GameRoot.Instance != root) return;
            if (requestHandler != null) events.Unsubscribe(NavigationRequestedEventArgs.EventId, requestHandler);
            if (completeHandler != null) events.Unsubscribe(NavigationCompletedEventArgs.EventId, completeHandler);
            requestHandler = completeHandler = null;
            events = null;
        }

        private void OnDestroy()
        {
            Application.logMessageReceivedThreaded -= CaptureLog;
            Unsubscribe();
            profile?.Dispose();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
