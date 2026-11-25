# Tutorials and obstacle courses

## Regular obstacle courses

Choose **Select Level** in the main menu to see the regular courses alongside the tutorials. In Unity, **FPVR > Courses > Play Obstacle Courses** opens the regular scene's own selector for testing. Every level is available immediately. During flight, the HUD shows the next checkpoint, active flight times, altitude, and crash count.

| Course | Difficulty | Route |
| --- | --- | --- |
| Meadow Slalom | Introductory | Five wide gates around alternating field barriers |
| Hangar Elevation | Intermediate | Seven gates at varied heights, with cargo stacks and an overhead beam |
| Canyon Circuit | Advanced | Nine narrower gates through rock pillars, ridges, and a high bridge |

Pass the highlighted checkpoints in order and then cross the separate finish. Regular courses use the same pause, recovery, scoring, review, and local result rules described below. Edit their routes in `Assets/FPVR/Tutorials/Runtime/ObstacleCourseCatalog.cs`.

The scene is `Assets/FPVR/Tutorials/Scenes/FPVR_ObstacleCourses.unity`.

Repeat the scene and route clearance checks with **FPVR > Courses > Validate Obstacle Courses**. Run **FPVR > Courses > Run Play Mode Smoke Checks** to exercise all three layouts, physical obstacle collisions, checkpoint order, recovery, finish, review, and retry.

## Guided practice

The guided practice scene is `Assets/FPVR/Tutorials/Scenes/FPVR_Tutorials.unity`. Use **FPVR > Tutorials > Open Tutorial Scene** to inspect it or **FPVR > Tutorials > Play Tutorial Scene** to test it directly. The Play shortcut temporarily overrides the editor's startup scene and restores it when you stop.

The tutorial selection offers free flight plus three short courses. All are available immediately. Meadow and Practice Hangar provide two simple, visually distinct environments. Each course has at least two ordered checkpoints and a separate finish; the difficulty labels describe their relative progression while the geometry remains beginner-friendly.

The committed tutorial scene contains its own saved copy of the existing drone and pilot rig. Tutorial geometry, gates, labels, and instructions are created at runtime by the tutorial scripts. It does not run `TrainingDemo` or change the shared flight scripts.

## Playing

Read the control and safety guide before starting. It reflects the drone's configured stick mode; the tutorial does not change flight physics or the selected flight-control configuration. Instructions remain visible during each course.

- Navigate tutorial screens with keyboard arrows and Enter, a gamepad D-pad and A, or the Quest left stick and A. Desktop users can also click the menu buttons. The same menu content is shown in the headset.
- Pause with Escape/P, gamepad Start, or Quest B. Resume, restart, change lesson, and leave from the tutorial screens without physically walking.
- Existing drone reset inputs (keyboard R, gamepad Y, Quest X) mean **Restart Level** in tutorials. Restart clears the attempt. Use **Respawn** on the crash screen to keep completed checkpoints, total time, and crashes while resetting section time.
- Complete the highlighted checkpoints in order, then fly through the finish. Passing later checkpoints early does not count.

Free flight has no checkpoints, completion timer, score, or rating. Courses record active flight time only. Guide, pause, crash recovery, and review screens do not add flight time. Completion freezes the result and shows the standard review even if local saving fails.

Completed course results are saved locally as guest results in `Application.persistentDataPath/tutorial-results.json`. They are local records, not synchronized account results. No online service is needed to play or review a result. The existing file is replaced only after the new JSON has been written to a temporary file; a save failure leaves review available and displays an explanation.

## Main menu integration

**Select Level** shows every activity from the tutorial and regular course catalogs. Choosing one opens its briefing. **Choose another level** returns to the combined list and **Return to main menu** goes to the startup screen. The **Demo course** option loads the original training scene.

Both flight scenes are enabled in the shared build scene list. If a build profile overrides the shared list, enable both flight scenes and keep `MainMenu` first. The direct editor shortcuts still start at the selector for their respective scene.

## Editing a lesson

Change route positions and per-checkpoint instructions in `Assets/FPVR/Tutorials/Runtime/TutorialCourseCatalog.cs`. `TutorialWorld.cs` builds the wide gate openings and simple environments. Keep checkpoint positions clear of frames and walls because each completed checkpoint is also a recovery position. Keep a separate finish after the last checkpoint. No menu or flight-controller edits are needed to change a route.

## Automated verification

Verified with Unity **6000.6.4f1** on 2026-11-24:

- Compilation, catalog/attempt model checks, and saved-scene reference validation passed.
- Play Mode checks passed for all courses, ordered and repeated passages, early finish rejection, pause, respawn, restart, completion review, and unassessed free flight.
- Forty synthetic crash/recovery cycles passed: twenty each in Meadow and Practice Hangar. These exercise session state and recovery placement, independently of flight skill.
- Real physics checks passed for harmless grounded launch, a low-speed landing after takeoff, and gentle gate-frame contact. Each valid crash was counted once.
- Rendered head-visible selection, guide, and course panels were inspected. Their text fits the backdrop. Actual headset readability, comfort, controller operation, and target frame rate remain unverified.

Run **FPVR > Tutorials > Validate Tutorial Content** and **FPVR > Tutorials > Run Play Mode Smoke Checks** to repeat the automated checks. The Play Mode runner temporarily backs up and restores its exact local result files. Batch entry points are `FPVR.Tutorials.EditorTools.TutorialEditor.Validate` (with `-quit`) and `FPVR.Tutorials.EditorTools.TutorialPlayChecks.Run` (without `-quit`; it exits with a success/failure status). Logs and optional rendered previews are under the ignored `Logs/` directory. Unity's Search database emitted an editor startup indexing exception during batch Play Mode; the tutorial checks completed successfully with exit code 0.

## Technical document traceability

The authoritative requirements and design sources are in the parent repository under `docs/`. This feature implements the tutorial-related slice; it does not establish whole-application compliance with requirements for accounts, global rankings, optional feedback, deployment, or measured headset performance.

| Source | Tutorial behavior / verification |
| --- | --- |
| SRS FR-LVL-01--02 | Free-flight option is unassessed; recovery returns to its start without restarting the application. |
| SRS FR-LVL-03--05; MIS M5 | Three immediately selectable, distinct courses labelled Introductory, Intermediate, Advanced; at least two environments. |
| SRS FR-LVL-06; FR-UI-05 | Sessions launched from the main menu offer a return to the combined level list and the startup screen. |
| SRS FR-LVL-07; FR-CHK-07; MIS M4 | New attempts and restarts clear progress, total time, section time, crashes, and previous evaluation. |
| SRS FR-CHK-01--03/08; MIS M7 | Ordered checkpoints form a completed prefix; duplicates and skipped checkpoints do not advance it. A separate finish requires all checkpoints. |
| SRS FR-CHK-04--06; FR-PER-04--05 | Crash screen offers Respawn and Restart Level. Respawn uses last completed checkpoint or start, retains total time/crashes/progress, and clears section time. |
| SRS FR-FLT-04--06; MIS M6 | Tutorial-only contact adapter recognizes obstacle contact and ground contact after takeoff, latches one crash, and suspends flight until recovery. Ground contact before takeoff is allowed. |
| SRS FR-PER-01--07/10; MIS M4/M8 | Active-flight total/section timing, one count per crash, paused menus, and frozen completion metrics. |
| SRS FR-PER-08--09 | Score is `max(0, 1000 - floor(totalSeconds) - 100 * crashes)`. Ratings: Gold >=800, Silver 600--799, Bronze 400--599, Needs Improvement 0--399. |
| SRS FR-UI-01/03--06; NFR-USAB-01 | Head-visible instructions and menus, control guide before flight, stationary keyboard/gamepad/Quest navigation. Actual controller and headset behavior requires manual testing. |
| SRS FR-REV-01--04 | Immediate standard completion review shows level, total time, checkpoint count, crashes, score, rating; retry and selection actions remain offline. |
| SRS FR-DAT-01; NFR-OFF-01 | Completed local guest record contains level id, timestamp, total time, crash count, score, rating, local player id. No account synchronization is claimed. |
| MG training/course/progress/metrics/UI separation; MIS M4--M8 | Tutorial coordination, catalog, attempt state, presentation, and persistence are separate from shared flight controls and the existing demo. |
| Hazard Analysis SR1/SR2/SR23 | Preflight safety guidance, seated/stationary operation, accessible pause/exit, and simulator limitations are explained. |
| Hazard Analysis SR3/SR4 | Tutorial flight suspends on focus or observed XR tracking/controller loss; restore hardware before resuming. Standalone requires motion controllers; PCVR also permits a gamepad with a tracked headset. This is tutorial-scene protection, not an application-wide XR service. |
| Hazard Analysis SR21/SR24 | Break reminder after 30 minutes; suggestion to practise in free flight after three crashes in the same checkpoint section. |

The SRS is the acceptance baseline where numerical performance targets conflict with older Hazard Analysis text: average 72 FPS over 120 seconds per environment (NFR-PERF-01), and response within 100 ms for at least 95 of 100 measured input events (NFR-PERF-02). Neither can be confirmed by source inspection alone.

## Manual validation

Run these checks in the Unity editor first, then on a supported Quest standalone build and PCVR with a gamepad. Record Unity version, target hardware, controller configuration, branch/commit, and any failures.

Use **FPVR > Tutorials > Validate Tutorial Content** for the automated course/attempt checks and saved-scene reference checks. It does not certify headset rendering, controller behavior, or measured performance.

1. **Selection and guide:** On a fresh session, select every option without completing another. Confirm the three difficulty labels, distinct layouts, two environments, readable guide before movement, and an accessible guide from tutorial selection. Repeat with both values of `droneStyleSticks` and compare the printed bindings with actual flight inputs.
2. **Free flight:** Fly and recover from a crash. Confirm no checkpoint objectives, running completion timer, score, or rating, and that recovery returns to start.
3. **Ordered progression:** In each course, pass checkpoint 2 before 1, revisit completed checkpoints, and reach the finish early. Progress must stay unchanged. Pass every checkpoint in order and then finish; only that run completes.
4. **Recovery:** Check harmless initial ground contact, a small takeoff followed by landing, and gentle obstacle contact. Crash before checkpoint 1, then after checkpoint 1. Confirm one crash per event, suspended flight while the crash screen is shown, start/last-checkpoint respawn with zero velocity, preserved total time/progress/crashes, and zero section time after respawn. Inspect every respawn location for obstacle clearance.
5. **Restart:** After progress, elapsed time, and crashes, use Restart Level. Confirm start position, zero checkpoints, both timers zero, zero crashes, and no previous review. Repeat with R, gamepad Y, and Quest X.
6. **Timing:** Fly 8 seconds, complete a checkpoint, fly 5 seconds, crash, wait in the crash screen, and respawn. Total should retain approximately 13 seconds while section returns to zero. Pause for a measured interval; neither timer may advance. Completion freezes both the result time and score.
7. **Score and review:** Check a deterministic sample: total `95.8`, crashes `2` gives `705` and Silver. Check rating boundaries 399/400, 599/600, 799/800 and score clamping at zero. Verify all review fields and retry/selection actions.
8. **Local persistence:** Complete a course offline, inspect the saved guest result for required fields, restart the application, and confirm the record remains. Exercise a save failure and confirm the review still appears with a useful save message. Exiting or crashing without completion must not create a completed record.
9. **Stationary/headset flow:** While seated, use only each supported controller to select, read, start, pause, recover, restart, review, and leave. Repeat standing without walking. Check readability and reachability with the headset on, including the longest crash message and the full control guide. Test PCVR with a tracked headset and gamepad while motion controllers are inactive. Repeat hardware/tracking loss checks during flight and before resuming. During the release-controls warmup, verify Cancel with keyboard, gamepad and Quest inputs even while a stick is held or tracking is unavailable.
10. **Reliability and performance:** Run 20 crash/respawn cycles in each environment (NFR-REL-01), checking checkpoint consistency throughout. Measure the SRS frame-rate/input thresholds on target hardware. Also verify the 30-minute break reminder and the repeated-crash practice suggestion.
11. **Application integration:** Start at the main menu and open each briefing from **Select Level**. From a briefing, pause screen, and completion review, test both return destinations. Verify that **Demo course** opens the original training scene.

Source-level/unit checks do not substitute for these Unity physics, input, rendering, persistence, and headset checks. Record untested hardware checks as unverified, rather than passed.
