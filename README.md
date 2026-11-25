# FPVR-Game

An FPV drone training game for Meta Quest, built in Unity. Fly a drone through a course of gates in first person, either in VR or on a regular screen.

## Requirements

- **Unity 6000.6.4f1** (install it through Unity Hub)
- **Android Build Support**, only if you want to build the standalone Quest app
- A **Meta Quest** headset and the **Meta Horizon Link** PC app, for VR

## Getting started

1. Clone the repo and open the folder in Unity Hub (**Add → Add project from disk**). The first import takes a few minutes.
2. Import the drone model from the Asset Store. See [Drone model (Asset Store)](#drone-model-asset-store) below.
3. Press **Play** in Unity, then choose **Select Level** in the FPVR menu. Choose a tutorial, free flight, or a regular obstacle course to open its briefing. **Demo course** opens the original training course.

Open `Assets/FPVR/Scenes/MainMenu.unity` to edit the startup menu in the Hierarchy. The scene list in **File → Build Profiles** starts with `MainMenu` and includes both level scenes and the original `FPVR_Demo` scene.

Built players use Unity's built-in splash. **Show Splash Screen** and **Show Unity Logo** are already enabled under **Edit → Project Settings → Player → Splash Image**, where **Preview** shows the splash in the Game view.

## Drone model (Asset Store)

The drone uses the free [Realistic Drone](https://assetstore.unity.com/packages/3d/vehicles/air/realistic-drone-66698) package by AnanasProject. It is licensed under the Standard Unity Asset Store EULA, which doesn't allow sharing its files in a public repository. It is listed in `.gitignore`, so **each teammate has to import it themselves**:

1. Open the [Realistic Drone page](https://assetstore.unity.com/packages/3d/vehicles/air/realistic-drone-66698) and click **Add to My Assets**. It's free.
2. In Unity, open **Window → Package Manager**, then select **My Assets** on the left.
3. Find **Realistic Drone**, click **Download**, then click **Import**. Keep everything ticked and click **Import** again.
4. Run **FPVR → Fix Realistic Drone Asset** from Unity's top menu. This makes two changes:
   - It converts the model's materials to URP, so they don't render pink.
   - It fixes an old script in the package (`screen.cs`) that otherwise stops the whole project from compiling.

If you skip this step, the game still runs. It shows the simple built-in box drone instead.

Don't commit anything from `Assets/Realistic Drone/`.

## Controls

The motors start as soon as you touch any flight control.

| Action | Keyboard | Gamepad | Quest controllers |
|---|---|---|---|
| Pitch | W / S (or ↑ ↓) | Right stick up/down | Right thumbstick up/down |
| Yaw | A / D (or ← →) | Left stick left/right | Left thumbstick left/right |
| Roll | Q / E | Right stick left/right | Right thumbstick left/right |
| Throttle | Space up, Shift or Left Ctrl down | Left stick up/down | Left thumbstick up/down |
| Reset | R | Y | X |
| Chase camera (non-VR) | C | | |
| Open or close flight settings | Esc | | Menu / options button |

Gamepad and Quest controllers use the Mode 2 stick layout.

Use the flight settings to adjust pitch, roll, yaw, and throttle sensitivity. Each slider starts at 100%, and changes are saved for future flights. On desktop, drag the sliders or use the arrow keys. In VR, move a thumbstick up or down to select a row and left or right to adjust it. Press the right controller's A button to activate the selected option.

In VR flight, the headset shows a fixed feed from the drone's camera. Turning or leaning your head keeps the same view from the drone. Turn on **Reduce Motion Sickness** in VR flight settings to add a soft, translucent border around the flight image. The setting is off by default and is saved for future VR flights.

## Playing in VR (Quest Link)

This runs the game from the Unity editor on your PC and streams it to the headset.

1. Install the **Meta Horizon Link** app on your PC and sign in with the same account as the headset.
2. In the Link app, go to **Settings → General** and turn on **Unknown Sources**. Without this, Meta blocks the game because it hasn't been through the Quest Store review.
3. Connect the headset with a USB-C cable (or Air Link). In the headset, open **Quick Settings → Link** and connect to your PC.
4. Press **Play** in Unity. In the headset, point either controller at **Select Level** and squeeze and release the trigger. Use the same trigger to pick an activity from the level list.

OpenXR and the Quest controller profiles are switched on automatically by `Assets/FPVR/Editor/EnableQuestControllers.cs`. If VR doesn't start, run **FPVR → Set Up Quest VR** and restart Unity.

For a standalone Quest APK, install Android Build Support and select the Android or Meta Quest build target. Under **XR Plug-in Management → OpenXR** for Android, enable **Meta Quest Support** and add the **Oculus Touch Controller Profile**.

## Project layout

| Path | What's there |
|---|---|
| `Assets/FPVR/Scenes/MainMenu.unity` | Editable startup menu with desktop and Quest input |
| `Assets/FPVR/Scripts/DroneFlight.cs` | Flight physics and input (keyboard, gamepad, VR) |
| `Assets/FPVR/Scripts/DroneView.cs`, `VrFpvDisplay.cs` | FPV and chase cameras and the tracked VR display |
| `Assets/FPVR/Scripts/TrainingDemo.cs` | Gate course, timer, HUD, propeller spin |
| `Assets/FPVR/Scripts/TrainingGate.cs` | Gate pass detection and highlighting |
| `Assets/FPVR/Scripts/SceneVisuals.cs` | Sky, lighting, post-processing and the grass field (applied at runtime) |
| `Assets/FPVR/Scripts/DroneAudio.cs`, `MotorSynth.cs` | Procedural sound: propellers, wind, coin and jackpot sounds |
| `Assets/FPVR/Scripts/DroneModel.cs` | Swaps the box drone for the imported 3D model |
| `Assets/FPVR/Editor/` | Editor helpers for startup, Quest VR setup, and asset fixes |

## Performance notes

On a standalone Quest, screen-space ambient occlusion and bloom are expensive. If the frame rate drops, untick **Screen Space Ambient Occlusion** in `Assets/FPVR_URP_Renderer.asset`.
