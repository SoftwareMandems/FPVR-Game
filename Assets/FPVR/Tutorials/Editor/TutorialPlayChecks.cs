using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FPVR.Tutorials.EditorTools
{
    /// <summary>
    /// Scene-level smoke checks using the real session and gate passage detection.
    /// Run in batch mode without -quit; this runner exits after Play Mode finishes.
    /// </summary>
    [InitializeOnLoad]
    public static class TutorialPlayChecks
    {
        const string Key = "FPVR.TutorialPlayChecks.";
        static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
        static Await waiting;

        sealed class Await
        {
            public readonly Func<bool> Condition;
            public readonly string Description;
            public readonly double Deadline;
            public Await(Func<bool> condition, string description, double timeout = 10)
            { Condition = condition; Description = description; Deadline = EditorApplication.timeSinceStartup + timeout; }
        }

        static TutorialPlayChecks()
        {
            EditorApplication.playModeStateChanged += StateChanged;
            EditorApplication.update += Update;
        }

        [MenuItem("FPVR/Tutorials/Run Play Mode Smoke Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start tutorial smoke checks from Edit Mode.");
            if (SessionState.GetBool(Key + "active", false))
                throw new InvalidOperationException("Tutorial smoke checks are already active.");
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TutorialEditor.ScenePath);
            if (!scene) throw new InvalidOperationException("Build the tutorial scene before running its smoke checks.");
            // Save only the two exact files this test can overwrite, and retain snapshots
            // through the Play Mode domain reload. No user history is left changed.
            Backup(TutorialResultStore.Path, "results");
            Backup(TutorialResultStore.Path + ".tmp", "temporary");
            SessionState.SetString(Key + "previousStart", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetInt(Key + "result", -1);
            SessionState.SetFloat(Key + "started", (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(Key + "active", true);
            EditorSceneManager.playModeStartScene = scene;
            EditorApplication.isPlaying = true;
        }

        static void Backup(string path, string name)
        {
            SessionState.SetString(Key + name + ".path", path);
            bool exists = File.Exists(path);
            SessionState.SetBool(Key + name + ".exists", exists);
            SessionState.SetString(Key + name + ".bytes", exists ? Convert.ToBase64String(File.ReadAllBytes(path)) : "");
        }

        static void Restore(string name)
        {
            string path = SessionState.GetString(Key + name + ".path", "");
            if (string.IsNullOrEmpty(path)) return;
            if (SessionState.GetBool(Key + name + ".exists", false))
                File.WriteAllBytes(path, Convert.FromBase64String(SessionState.GetString(Key + name + ".bytes", "")));
            else if (File.Exists(path)) File.Delete(path);
            SessionState.EraseString(Key + name + ".path");
            SessionState.EraseString(Key + name + ".bytes");
            SessionState.EraseBool(Key + name + ".exists");
        }

        static void StateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key + "active", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                steps.Clear(); waiting = null; steps.Push(CheckScene());
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                int result = SessionState.GetInt(Key + "result", -1);
                if (result < 0) { Debug.LogError("Tutorial smoke checks were interrupted."); result = 1; }
                try { Restore("results"); Restore("temporary"); }
                catch (Exception exception) { Debug.LogException(exception); result = 1; }
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    SessionState.GetString(Key + "previousStart", ""));
                SessionState.EraseBool(Key + "active");
                SessionState.EraseInt(Key + "result");
                SessionState.EraseFloat(Key + "started");
                SessionState.EraseString(Key + "previousStart");
                steps.Clear(); waiting = null;
                if (result == 0) Debug.Log("TUTORIAL PLAY CHECKS PASSED");
                if (Application.isBatchMode) EditorApplication.Exit(result);
            }
        }

        static void Update()
        {
            if (!SessionState.GetBool(Key + "active", false)) return;
            if (SessionState.GetInt(Key + "result", -1) >= 0) return;
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "started", 0) > 180)
                    throw new TimeoutException("Tutorial smoke checks exceeded three minutes.");
                if (!EditorApplication.isPlaying || steps.Count == 0) return;
                if (waiting != null)
                {
                    if (!waiting.Condition())
                    {
                        if (EditorApplication.timeSinceStartup > waiting.Deadline)
                            throw new TimeoutException("Timed out: " + waiting.Description);
                        return;
                    }
                    waiting = null;
                }
                while (steps.Count > 0)
                {
                    var step = steps.Peek();
                    if (!step.MoveNext()) { steps.Pop(); continue; }
                    if (step.Current is IEnumerator child) { steps.Push(child); continue; }
                    waiting = step.Current as Await ?? NextFrame();
                    return;
                }
                Finish(true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(false);
            }
        }

        static void Finish(bool success)
        {
            SessionState.SetInt(Key + "result", success ? 0 : 1);
            steps.Clear(); waiting = null;
            EditorApplication.isPlaying = false;
        }

        static Await NextFrame()
        {
            int frame = Time.frameCount;
            return new Await(() => Time.frameCount > frame, "next rendered game frame");
        }

        static Await Delay(double seconds)
        {
            double finish = EditorApplication.timeSinceStartup + seconds;
            return new Await(() => EditorApplication.timeSinceStartup >= finish, "short stability interval");
        }

        static Await Flying(TutorialSession session) => new Await(
            () => session.Attempt != null && session.Attempt.Phase == TutorialPhase.Flying,
            "neutral-input warmup and transition to flight");

        static void Capture(string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Directory.CreateDirectory("Logs");
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("Logs/" + name + ".png"));
            // Batch mode has no Game view screenshot surface. Render the actual head-visible
            // panel with its scene camera so text fit and geometry can still be inspected.
            var session = UnityEngine.Object.FindAnyObjectByType<TutorialSession>();
            var hud = session.GetComponent<TutorialHud>();
            var camera = session.pilot.view;
            bool wasVR = hud.IsVR; var previousTarget = camera.targetTexture;
            var target = new RenderTexture(1280, 800, 24);
            var pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            var previousActive = RenderTexture.active;
            try
            {
                hud.IsVR = true; hud.RefreshHeadsetPanel();
                camera.targetTexture = target; camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); pixels.Apply();
                File.WriteAllBytes("Logs/" + name + "-panel.png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                hud.IsVR = wasVR; hud.RefreshHeadsetPanel();
                UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(target);
            }
        }

        // Kinematic positioning exercises the real scene's segment/gate tests without
        // making these smoke checks depend on the shared flight controller's physics.
        static void Freeze(TutorialSession session)
        {
            session.drone.enabled = false;
            if (!session.drone.Body.isKinematic)
            {
                session.drone.Body.linearVelocity = Vector3.zero;
                session.drone.Body.angularVelocity = Vector3.zero;
            }
            session.drone.Body.isKinematic = true;
            session.drone.armed = true;
        }

        static void Place(TutorialSession session, Vector3 position)
        {
            Freeze(session);
            session.drone.Body.position = position;
            session.drone.transform.position = position;
            Physics.SyncTransforms();
        }

        static IEnumerator Cross(TutorialSession session, Vector3 gate)
        {
            // Move around the outside of every gate before approaching its front.
            // A straight teleport to a later gate could legitimately pass earlier ones.
            Place(session, new Vector3(20, gate.y, session.drone.transform.position.z));
            yield return NextFrame();
            Place(session, new Vector3(20, gate.y, gate.z - .5f));
            yield return NextFrame();
            Place(session, gate - Vector3.forward * .5f);
            yield return NextFrame();
            Place(session, gate + Vector3.forward * .5f);
            yield return NextFrame();
        }

        static Await PhysicsSteps(int count)
        {
            float target = Time.fixedTime + Time.fixedDeltaTime * count;
            return new Await(() => Time.fixedTime >= target, "real physics steps", 2);
        }

        static IEnumerator CheckPhysicalCollisions(TutorialSession session)
        {
            session.SelectCourse(1); session.Choose(0);
            yield return Flying(session);
            var drone = session.drone;
            var body = drone.Body;
            var collider = drone.GetComponent<Collider>();
            Require(drone.enabled && !body.isKinematic && !drone.armed,
                "Physical checks must begin with the live, disarmed flight controller and dynamic body.");
            yield return new Await(() => collider.bounds.min.y <= .065f && Mathf.Abs(body.linearVelocity.y) < .08f,
                "drone settling harmlessly on the launch pad", 2);
            yield return PhysicsSteps(3);
            Require(session.Attempt.Phase == TutorialPhase.Flying && !drone.crashed && session.Attempt.Crashes == 0,
                "Initial contact with the launch pad before takeoff must not register a crash.");

            // A short, real upward motion lets TutorialContact detect takeoff. Then
            // disarm and let gravity return the dynamic drone to the launch pad.
            // The small height makes impact slower than the shared controller's
            // crash-speed threshold, exercising the tutorial's airborne-ground rule.
            Vector3 takeoff = body.position + Vector3.up * .35f;
            body.position = takeoff; drone.transform.position = takeoff;
            body.linearVelocity = Vector3.up * .3f; body.angularVelocity = Vector3.zero;
            drone.armed = true; body.WakeUp(); Physics.SyncTransforms();
            yield return PhysicsSteps(3);
            Require(!drone.crashed && session.Attempt.Phase == TutorialPhase.Flying,
                "Brief upward flight clear of the pad must remain valid.");
            drone.armed = false;
            yield return new Await(() => session.Attempt.Phase == TutorialPhase.Crashed,
                "low-speed landing collision after takeoff", 2);
            Require(session.Attempt.Crashes == 1 && drone.crashed,
                "Landing after takeoff must produce one real collision crash even when disarmed.");
            yield return Delay(.1);
            Require(session.Attempt.Crashes == 1, "Persistent landing contact must not duplicate the crash event.");

            session.SelectCourse(1); session.Choose(0);
            yield return Flying(session);
            drone = session.drone; body = drone.Body;
            Require(drone.enabled && !body.isKinematic, "Obstacle collision must use the live dynamic drone.");
            // Overlap the near face of the left frame very slightly. A nearly
            // stationary, disarmed contact must still trigger tutorial recovery.
            Vector3 obstacle = session.Course.Checkpoints[0] + new Vector3(-3.35f, 0, -.37f);
            body.position = obstacle; drone.transform.position = obstacle;
            body.linearVelocity = Vector3.forward * .05f; body.angularVelocity = Vector3.zero;
            drone.armed = false; body.WakeUp(); Physics.SyncTransforms();
            yield return new Await(() => session.Attempt.Phase == TutorialPhase.Crashed,
                "gentle real gate-frame contact", 2);
            Require(session.Attempt.Crashes == 1 && session.Attempt.Passed == 0 && drone.crashed,
                "Gentle gate-frame contact must register exactly one crash and no checkpoint progress.");
            yield return Delay(.1);
            Require(session.Attempt.Crashes == 1, "Persistent obstacle contact must not duplicate the crash event.");
            Debug.Log("TUTORIAL PHYSICS COLLISION CHECKS PASSED: harmless grounded launch, " +
                "low-speed landing after takeoff, and gentle obstacle contact with the live dynamic drone.");
        }

        static IEnumerator CheckScene()
        {
            TutorialSession session = null;
            yield return new Await(() => (session = UnityEngine.Object.FindAnyObjectByType<TutorialSession>()) && session.Ready,
                "tutorial scene session initialization");
            Require(session.Selecting && session.Attempt == null, "Scene must open at the activity selector.");
            Require(!session.drone.enabled && session.drone.Body.isKinematic, "Selection must freeze flight.");
            yield return NextFrame(); Capture("tutorial-selection"); yield return NextFrame();
            session.SelectCourse(1);
            Require(session.Attempt.Phase == TutorialPhase.Guide, "Selecting a course must show its control guide.");
            Vector3 guidePosition = session.drone.transform.position;
            yield return Delay(.1);
            Require(session.Attempt.TotalTime == 0 && session.drone.transform.position == guidePosition,
                "The control guide must freeze flight and timing.");
            Capture("tutorial-guide"); yield return NextFrame();
            session.Choose(0);
            yield return Flying(session);
            Freeze(session);
            yield return NextFrame();
            Require(session.Attempt.TotalTime > 0, "Flight must start the course timer.");

            yield return Cross(session, session.Course.Checkpoints[1]);
            Require(session.Attempt.Passed == 0, "Crossing a later gate must not advance progress.");
            yield return Cross(session, session.Course.Finish);
            Require(session.Attempt.Passed == 0 && session.Attempt.Phase == TutorialPhase.Flying,
                "Early finish must not complete a course.");
            yield return Cross(session, session.Course.Checkpoints[0]);
            Require(session.Attempt.Passed == 1, "The next required checkpoint must count.");
            yield return Cross(session, session.Course.Checkpoints[0]);
            Require(session.Attempt.Passed == 1, "A checkpoint must count only once.");

            session.drone.crashed = true;
            yield return NextFrame();
            Require(session.Attempt.Phase == TutorialPhase.Crashed && session.Attempt.Crashes == 1,
                "A drone crash must open recovery and record one crash.");
            float total = session.Attempt.TotalTime;
            yield return Delay(.15);
            Require(session.Attempt.TotalTime == total && session.Attempt.Crashes == 1,
                "Recovery time must not count, and the same crash must not be counted twice.");
            Capture("tutorial-crash");
            session.Respawn();
            yield return Flying(session);
            Freeze(session);
            Require(session.Attempt.Passed == 1 && session.Attempt.Crashes == 1 &&
                session.Attempt.TotalTime >= total && session.Attempt.TotalTime - total < .25f && session.Attempt.SectionTime < .25f,
                "Respawn must retain progress, total time and penalties while restarting section timing.");
            Require(Vector3.Distance(session.drone.transform.position, session.Course.Checkpoints[0]) < .1f,
                "Respawn must return to the most recent completed checkpoint.");

            session.PauseFlight("Smoke check pause");
            yield return NextFrame();
            total = session.Attempt.TotalTime;
            yield return Delay(.15);
            Require(session.Attempt.Phase == TutorialPhase.Paused && session.Attempt.TotalTime == total &&
                !session.drone.enabled && session.drone.Body.isKinematic, "Pause must freeze physics and timers.");
            session.Choose(0);
            yield return Flying(session);
            Freeze(session);
            session.RestartLevel();
            Require(session.Attempt.Phase == TutorialPhase.Guide && session.Attempt.Passed == 0 &&
                session.Attempt.Crashes == 0 && session.Attempt.TotalTime == 0 && session.Attempt.SectionTime == 0,
                "Restart must clear the entire attempt and return to its guide.");
            Require(Vector3.Distance(session.drone.transform.position, session.Course.Start) < .01f,
                "Restart must return to the level start.");

            for (int course = 1; course < TutorialCourseCatalog.All.Length; course++)
            {
                session.SelectCourse(course); session.Choose(0);
                yield return Flying(session); Freeze(session);
                if (course == 3) { yield return NextFrame(); Capture("tutorial-hangar"); yield return NextFrame(); }
                for (int checkpoint = 0; checkpoint < session.Course.Checkpoints.Length; checkpoint++)
                {
                    yield return Cross(session, session.Course.Checkpoints[checkpoint]);
                    Require(session.Attempt.Passed == checkpoint + 1, "Every course must support ordered checkpoint passage.");
                }
                Require(session.Attempt.Phase == TutorialPhase.Flying, "The last checkpoint must not replace the finish gate.");
                yield return Cross(session, session.Course.Finish);
                Require(session.Attempt.Phase == TutorialPhase.Complete, "Each course must complete at its separate finish.");
                total = session.Attempt.TotalTime;
                yield return Delay(.1);
                Require(session.Attempt.TotalTime == total && session.drone.Body.isKinematic,
                    "Completion must stop timing and flight.");
            }

            // Repeated recovery in both environments catches stale objects and state.
            foreach (int course in new[] { 1, 3 })
            {
                session.SelectCourse(course); session.Choose(0);
                yield return Flying(session); Freeze(session);
                for (int crash = 1; crash <= 20; crash++)
                {
                    session.drone.crashed = true;
                    yield return NextFrame();
                    Require(session.Attempt.Phase == TutorialPhase.Crashed && session.Attempt.Crashes == crash,
                        "Repeated recovery must count each crash once in " + session.Course.Environment);
                    session.Respawn();
                    yield return Flying(session); Freeze(session);
                    Require(session.Attempt.Passed == 0 && Vector3.Distance(session.drone.transform.position, session.Course.Start) < .1f,
                        "Recovery before the first checkpoint must return to the start.");
                }
            }
            Debug.Log("TUTORIAL SYNTHETIC RECOVERY CHECKS PASSED: 40 cycles (20 Meadow, 20 Practice Hangar).");

            session.SelectCourse(0); session.Choose(0);
            yield return Flying(session); Freeze(session);
            yield return Delay(.2);
            Require(session.Course.FreeFlight && session.Attempt.TotalTime == 0 && session.Attempt.SectionTime == 0 &&
                session.Attempt.Score == 0 && !session.Attempt.Finish(), "Free flight must remain unassessed and have no finish.");
            session.drone.crashed = true;
            yield return NextFrame();
            session.Respawn();
            yield return Flying(session); Freeze(session);
            Require(session.Attempt.Crashes == 0 && Vector3.Distance(session.drone.transform.position, session.Course.Start) < .1f,
                "Free-flight recovery must restore the practice start without assessment.");
            yield return CheckPhysicalCollisions(session);
            session.ShowSelection();
            Require(session.Selecting, "The activity menu must remain reachable without restarting the application.");
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Tutorial Play Mode check failed: " + message);
        }
    }
}
