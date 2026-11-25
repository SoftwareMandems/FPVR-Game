using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace FPVR.Tutorials
{
    // Runs both guided practice and regular courses using the same flight and attempt rules.
    [DefaultExecutionOrder(-100)]
    public sealed class TutorialSession : MonoBehaviour
    {
        public DroneFlight drone;
        public DroneView pilot;
        public Transform[] rotors;
        [Tooltip("Show the regular obstacle course catalog instead of guided practice.")]
        public bool regularCourses;
        [Tooltip("Set to the main menu scene when integrating. Empty returns to level selection.")]
        public string mainMenuScene;
        [Tooltip("The active local profile. Guest practice works without an account.")]
        public string localPlayerId = "guest";
        public TutorialAttempt Attempt { get; private set; }
        public TutorialCourse Course { get; private set; }
        public bool Selecting => Attempt == null;
        public bool Ready => hud != null;
        TutorialCourse[] Courses => regularCourses ? ObstacleCourseCatalog.All : TutorialCourseCatalog.All;
        TutorialWorld world;
        TutorialHud hud;
        TutorialInput input;
        TutorialContact contact;
        DroneAudio sounds;
        Vector3 previous;
        bool relocating, warmup, resumeArmed, hadXR;
        float neutralTime, sessionTime;
        int selected, consecutiveCrashes;
        bool breakReminder;
        string launchMenuScene;
        Action queuedFlight;
        string notice = "", saveMessage = "";

        void Start()
        {
            if (!drone || !pilot || !pilot.view)
            { Debug.LogError("Flight session requires saved Drone and Pilot references.", this); enabled = false; return; }
            input = new TutorialInput();
            contact = drone.GetComponent<TutorialContact>();
            if (!contact) contact = drone.gameObject.AddComponent<TutorialContact>();
            sounds = drone.GetComponent<DroneAudio>();
            if (!sounds) sounds = drone.gameObject.AddComponent<DroneAudio>();
            world = gameObject.AddComponent<TutorialWorld>();
            hud = gameObject.AddComponent<TutorialHud>(); hud.Attach(pilot.view); hud.Choose = Choose;
            drone.ResetCourse += ResetRequested;
            Application.targetFrameRate = 90; Time.fixedDeltaTime = 1f / 90f;

            if (LevelSelectionNavigation.TryTakeCourse(gameObject.scene.path, out var courseId, out launchMenuScene))
            {
                int courseIndex = Array.FindIndex(Courses, course => course.Id == courseId);
                if (courseIndex >= 0)
                {
                    SelectCourse(courseIndex);
                    return;
                }
                Debug.LogWarning($"Level '{courseId}' was not found in {gameObject.scene.name}.", this);
            }

            ShowSelection();
        }

        public void ShowSelection()
        {
            CancelWarmup(); Attempt = null; Course = Courses[0];
            selected = 0; notice = ""; world.Load(Course); world.Highlight(0); MoveDrone(Course.Start, false); Present();
        }

        public void SelectCourse(int index)
        {
            if (index < 0 || index >= Courses.Length) throw new ArgumentOutOfRangeException(nameof(index));
            CancelWarmup(); Course = Courses[index];
            Attempt = new TutorialAttempt(Course.Checkpoints.Length, Course.FreeFlight);
            selected = consecutiveCrashes = 0; notice = saveMessage = "";
            world.Load(Course); world.Highlight(0); MoveDrone(Course.Start, false); Present();
        }

        void Suspend()
        {
            drone.enabled = false; drone.armed = false;
            if (!drone.Body.isKinematic)
            { drone.Body.linearVelocity = Vector3.zero; drone.Body.angularVelocity = Vector3.zero; }
            drone.Body.isKinematic = true;
        }
        void CancelWarmup() { warmup = false; queuedFlight = null; neutralTime = 0; Suspend(); }

        void MoveDrone(Vector3 position, bool airborne)
        {
            relocating = true;
            drone.ResetDrone();
            drone.Body.position = position; drone.Body.rotation = Course.StartRotation;
            drone.transform.SetPositionAndRotation(position, Course.StartRotation);
            relocating = false;
            contact.ResetContact(airborne); previous = position; Suspend();
        }

        void QueueFlight(Action transition, bool armed)
        {
            queuedFlight = transition; resumeArmed = armed; warmup = true; neutralTime = 0;
            notice = "Release the sticks and flight keys to continue.";
        }

        // Clear the controller's existing input smoothing while its rigidbody is frozen.
        // A neutral interval prevents a held menu-navigation stick from launching the drone.
        void WarmFlight()
        {
            if ((!Application.isFocused && !Application.isBatchMode) || ((hadXR || Application.platform == RuntimePlatform.Android) && !TutorialInput.XRReady()))
            { drone.enabled = false; neutralTime = 0; notice = "Restore headset tracking and the selected controllers before continuing."; return; }
            if (!input.FlightNeutral()) { drone.enabled = false; neutralTime = 0; return; }
            drone.armed = false; drone.Body.isKinematic = true; drone.enabled = true;
            neutralTime += Time.unscaledDeltaTime;
            if (neutralTime < .4f) return;
            warmup = false; var transition = queuedFlight; queuedFlight = null;
            transition?.Invoke();
            drone.Body.isKinematic = false; drone.armed = resumeArmed;
            previous = drone.transform.position; notice = "";
        }

        public void PauseFlight(string reason = "Flight paused. Take your time.")
        {
            if (Attempt == null || Attempt.Phase != TutorialPhase.Flying) return;
            resumeArmed = drone.armed; Attempt.Pause(); Suspend();
            selected = 0; notice = reason; Present();
        }

        public void Respawn()
        {
            if (Attempt == null || Attempt.Phase != TutorialPhase.Crashed) return;
            var position = Attempt.Passed == 0 ? Course.Start : Course.Checkpoints[Attempt.Passed - 1];
            MoveDrone(position, Attempt.Passed > 0);
            QueueFlight(() => Attempt.Respawn(), Attempt.Passed > 0);
        }

        public void RestartLevel()
        {
            if (Attempt == null) return;
            CancelWarmup(); Attempt.Restart(); consecutiveCrashes = selected = 0;
            saveMessage = notice = ""; MoveDrone(Course.Start, false); world.Highlight(0); Present();
        }

        void ResetRequested()
        {
            if (relocating || Attempt == null) return;
            // R / X / Y (and the shared controller's boundary reset) always mean a fresh attempt.
            RestartLevel();
        }

        void Update()
        {
            if (input == null) return;
            hadXR |= XRSettings.isDeviceActive;
            input.Poll();
            if (input.Restart && Attempt != null) { RestartLevel(); return; }
            if (warmup && (input.Pause || input.Confirm))
            { CancelWarmup(); Present(); return; }
            sessionTime += Time.unscaledDeltaTime;
            if (!breakReminder && sessionTime >= 1800 && Attempt != null && Attempt.Phase == TutorialPhase.Flying)
            { breakReminder = true; PauseFlight("You have practised for 30 minutes. Take a break before continuing."); }
            if (Attempt != null && Attempt.Phase == TutorialPhase.Flying)
            {
                if (drone.crashed) RegisterCrash();
                else if ((!Application.isFocused && !Application.isBatchMode) || ((hadXR || Application.platform == RuntimePlatform.Android) && !TutorialInput.XRReady()))
                    PauseFlight("Tracking, focus or a controller was lost. Restore it, then choose Resume.");
                else if (input.Pause) PauseFlight();
            }
            else if (input.Pause && !warmup)
            {
                if (Attempt != null && Attempt.Phase == TutorialPhase.Paused) QueueFlight(() => Attempt.Resume(), resumeArmed);
                else if (Attempt != null) ChooseAnotherLevel();
            }
            if (warmup) WarmFlight();
            if (Attempt == null || Attempt.Phase != TutorialPhase.Flying)
            {
                if (!warmup)
                {
                    var options = MenuOptions();
                    if (options.Length > 0) selected = (selected + input.Move + options.Length) % options.Length;
                    if (input.Confirm) Choose(selected);
                }
            }
            Present();
        }

        void LateUpdate()
        {
            if (Attempt == null || Attempt.Phase != TutorialPhase.Flying) return;
            Attempt.Tick(Time.deltaTime);
            if (drone.crashed) { RegisterCrash(); Present(); return; }
            var now = drone.transform.position;
            if (!Course.FreeFlight)
            {
                // Only consume the next expected checkpoint, even if later gates were crossed.
                int passed = Attempt.Passed;
                while (Attempt.Passed < world.Gates.Length && world.Gates[Attempt.Passed].Crossed(previous, now, .29f))
                { Attempt.PassCheckpoint(Attempt.Passed); consecutiveCrashes = 0; sounds.PlayCoin(Attempt.Passed); }
                if (Attempt.Passed != passed) world.Highlight(Attempt.Passed);
                if (Attempt.Passed == world.Gates.Length && world.Finish.Crossed(previous, now, .29f) && Attempt.Finish())
                {
                    Suspend(); sounds.PlayJackpot(); selected = 0;
                    TutorialResultStore.TrySave(Course, Attempt, localPlayerId, out saveMessage);
                }
            }
            previous = now;
            if (drone.armed && rotors != null)
                for (int i = 0; i < rotors.Length; i++)
                    if (rotors[i]) rotors[i].Rotate(Vector3.up, (i % 2 == 0 ? 1 : -1) * 1800 * Time.deltaTime, Space.World);
            Present();
        }

        void RegisterCrash()
        {
            Attempt.Crash(); Suspend(); consecutiveCrashes++; selected = 0;
            if (consecutiveCrashes >= 3)
                notice = regularCourses ? "Three crashes in this section. Free flight can help you practise the manoeuvre."
                    : "Three crashes in this section. Try Free flight practice from Choose another level.";
            else
                notice = regularCourses ? "Choose a recovery option." : "No rush. Respawn keeps progress; Restart begins a new attempt.";
        }

        string[] MenuOptions()
        {
            if (warmup) return new[] { "Cancel" };
            if (Attempt == null)
            {
                var courses = Courses;
                var options = new string[courses.Length + (HasMainMenu ? 1 : 0)];
                for (int i = 0; i < courses.Length; i++)
                {
                    var course = courses[i];
                    options[i] = course.Name + (course.FreeFlight ? "" : " / " + course.Difficulty);
                }
                if (HasMainMenu) options[courses.Length] = "Return to main menu";
                return options;
            }
            switch (Attempt.Phase)
            {
                case TutorialPhase.Guide: return LevelOptions("Start flight");
                case TutorialPhase.Paused: return LevelOptions("Resume", "Restart level");
                case TutorialPhase.Crashed: return LevelOptions("Respawn", "Restart level");
                case TutorialPhase.Complete: return LevelOptions("Retry level");
                default: return Array.Empty<string>();
            }
        }
        bool HasMainMenu => !string.IsNullOrEmpty(launchMenuScene ?? mainMenuScene);

        string[] LevelOptions(params string[] actions)
        {
            var options = new string[actions.Length + (HasMainMenu ? 2 : 1)];
            Array.Copy(actions, options, actions.Length);
            options[actions.Length] = "Choose another level";
            if (HasMainMenu) options[actions.Length + 1] = "Return to main menu";
            return options;
        }

        void ChooseAnotherLevel()
        {
            if (launchMenuScene == null) ShowSelection();
            else Exit(showLevelSelection: true);
        }

        void Exit(bool showLevelSelection = false)
        {
            string destination = launchMenuScene ?? mainMenuScene;
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(destination);
            if (buildIndex >= 0)
            {
                CancelWarmup();
                if (showLevelSelection) LevelSelectionNavigation.QueueReturn(destination);
                SceneManager.LoadScene(buildIndex);
            }
            else if (!string.IsNullOrEmpty(destination) && Application.CanStreamedLevelBeLoaded(destination))
            {
                CancelWarmup();
                SceneManager.LoadScene(destination);
            }
            else
            {
                notice = "The main menu is unavailable in this build.";
                Present();
            }
        }
        public void Choose(int index)
        {
            if (warmup) { CancelWarmup(); Present(); return; }
            if (index < 0 || index >= MenuOptions().Length) return;
            selected = 0;
            if (Attempt == null)
            {
                if (index < Courses.Length) SelectCourse(index);
                else Exit();
                return;
            }
            switch (Attempt.Phase)
            {
                case TutorialPhase.Guide:
                    if (index == 0) QueueFlight(() => Attempt.BeginFlight(), false);
                    else if (index == 1) ChooseAnotherLevel(); else Exit();
                    break;
                case TutorialPhase.Paused:
                    if (index == 0) QueueFlight(() => Attempt.Resume(), resumeArmed);
                    else if (index == 1) RestartLevel(); else if (index == 2) ChooseAnotherLevel(); else Exit();
                    break;
                case TutorialPhase.Crashed:
                    if (index == 0) Respawn(); else if (index == 1) RestartLevel(); else if (index == 2) ChooseAnotherLevel(); else Exit();
                    break;
                case TutorialPhase.Complete:
                    if (index == 0) RestartLevel(); else if (index == 1) ChooseAnotherLevel(); else Exit();
                    break;
            }
            Present();
        }

        string Controls()
        {
            return "Use Space/Shift for throttle, W/S for pitch, Q/E for roll, and A/D for yaw.\n" +
                "Gamepad and Quest use Mode 2. On the left stick, up/down controls throttle and left/right controls yaw. " +
                "On the right stick, up/down controls pitch and left/right controls roll.\n" +
                "Restart with R, gamepad Y, or Quest X to clear the attempt.\n" +
                "Pause with P, Esc, gamepad Start, or Quest B." + (regularCourses ? "" : " Use small inputs.");
        }
        void Present()
        {
            if (!hud) return;
            hud.IsVR = XRSettings.isDeviceActive; hud.Selected = selected; hud.Options = MenuOptions();
            hud.Flying = Attempt != null && Attempt.Phase == TutorialPhase.Flying;
            if (Attempt == null)
            {
                hud.Title = regularCourses ? "FPVR / OBSTACLE COURSES" : "FPVR / SIMPLE TUTORIALS";
                hud.Body = (regularCourses ? "Choose a course. All difficulties are available."
                    : "Choose any activity. All levels are unlocked. Start with Free flight if you are new.") + "\n\n" +
                    "Stay seated or stand still in a clear play area. Pause if you feel uncomfortable. Take regular breaks.\n" +
                    "This simulator supports practice; it does not replace real flight training or local drone rules.";
            }
            else if (warmup)
            { hud.Title = "READY TO FLY"; hud.Body = notice; }
            else if (Attempt.Phase == TutorialPhase.Guide)
            {
                hud.Title = Course.Name + (regularCourses ? " / COURSE BRIEFING" : " / CONTROL GUIDE");
                hud.Body = Course.Introduction + "\n\n" + Controls() + "\n\nStay stationary. Pause or leave at any time.";
            }
            else if (hud.Flying)
            {
                hud.Title = Course.Name;
                string objective = regularCourses
                    ? (Attempt.Passed < Attempt.CheckpointCount ? "Next checkpoint: " + (Attempt.Passed + 1) : "Next: Finish")
                    : Course.Instructions[Mathf.Min(Attempt.Passed, Course.Instructions.Length - 1)];
                hud.Body = objective + "\n" +
                    (Course.FreeFlight ? "Free practice: no timer or score." : "Checkpoints " + Attempt.Passed + "/" + Attempt.CheckpointCount + "   Total " + Attempt.TotalTime.ToString("F1") + "s   Section " + Attempt.SectionTime.ToString("F1") + "s   Crashes " + Attempt.Crashes) +
                    "\nAltitude " + drone.transform.position.y.ToString("F1") + "m   P / Start / Quest B: pause and controls";
            }
            else if (Attempt.Phase == TutorialPhase.Complete)
            {
                hud.Title = (regularCourses ? "COURSE COMPLETE / " : "TUTORIAL COMPLETE / ") + Course.Name;
                hud.Body = "Time " + Attempt.TotalTime.ToString("F1") + "s   Checkpoints " + Attempt.Passed + "/" + Attempt.CheckpointCount +
                    "\nCrashes " + Attempt.Crashes + "   Score " + Attempt.Score + "   " + Attempt.Rating +
                    "\nScore = max(0, 1000 - floor(seconds) - 100 x crashes).\nRatings are game feedback, not a pilot qualification.\n\n" + saveMessage;
            }
            else
            {
                hud.Title = Attempt.Phase == TutorialPhase.Crashed ? "CRASH / CHOOSE RECOVERY" : "FLIGHT PAUSED";
                hud.Body = notice + "\n\n" + (Course.FreeFlight ? "Respawn returns to the practice start." :
                    "Checkpoints " + Attempt.Passed + "/" + Attempt.CheckpointCount + "   Total " + Attempt.TotalTime.ToString("F1") + "s   Crashes " + Attempt.Crashes + "\nRespawn retains progress, total time and penalties. Restart clears all.") + "\n\n" + Controls();
            }
            if (!hud.Flying && !warmup && !string.IsNullOrEmpty(notice) &&
                (Attempt == null || Attempt.Phase == TutorialPhase.Guide || Attempt.Phase == TutorialPhase.Complete))
                hud.Body += "\n\n" + notice;
        }
        void OnApplicationFocus(bool focused) { if (!focused && !Application.isBatchMode && Ready) PauseFlight("Application focus was lost. Choose Resume when ready."); }
        void OnApplicationPause(bool paused) { if (paused && Ready) PauseFlight("The headset paused. Choose Resume when ready."); }
        void OnDestroy()
        {
            input?.Dispose();
            if (drone) drone.ResetCourse -= ResetRequested;
        }
    }
}
