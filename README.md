# FPVR-Game

An FPV drone training game for Meta Quest, built in Unity. Fly a drone through a course of gates in first person, either in VR or on a regular screen.

## Requirements

- **Unity 6000.6.4f1** (install it through Unity Hub)
- **Android Build Support**, only if you want to build the standalone Quest app
- A **Meta Quest** headset and the **Meta Horizon Link** PC app, for VR

## Getting started

1. Clone the repo and open the folder in Unity Hub (**Add → Add project from disk**). The first import takes a few minutes.
2. Import the drone model from the Asset Store. See [Drone model (Asset Store)](#drone-model-asset-store) below.
3. Press **Play**. The demo scene (`Assets/FPVR/Scenes/FPVR_Demo.unity`) opens and plays automatically.

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
| Forward / back | W / S (or ↑ ↓) | Left stick | Left thumbstick |
| Turn | A / D (or ← →) | Right stick left/right | Right thumbstick left/right |
| Slide sideways | Q / E | Left stick | Left thumbstick |
| Up / down | Space / Shift | Right stick up/down, or triggers | Right thumbstick up/down, or triggers |
| Reset | R | Y | X |
| Chase camera (non-VR) | C | | |
| Recenter VR view | V | | |

To use real drone "Mode 2" sticks instead, tick **Drone Style Sticks** on the Drone object.

## Playing in VR (Quest Link)

This runs the game from the Unity editor on your PC and streams it to the headset.

1. Install the **Meta Horizon Link** app on your PC and sign in with the same account as the headset.
2. In the Link app, go to **Settings → General** and turn on **Unknown Sources**. Without this, Meta blocks the game because it hasn't been through the Quest Store review.
3. Connect the headset with a USB-C cable (or Air Link). In the headset, open **Quick Settings → Link** and connect to your PC.
4. Press **Play** in Unity.

OpenXR and the Quest controller profiles are switched on automatically by `Assets/FPVR/Editor/EnableQuestControllers.cs`. If VR doesn't start, run **FPVR → Set Up Quest VR** and restart Unity.

## Project layout

| Path | What's there |
|---|---|
| `Assets/FPVR/Scripts/DroneFlight.cs` | Flight physics and input (keyboard, gamepad, VR) |
| `Assets/FPVR/Scripts/DroneView.cs` | FPV and chase camera, VR head tracking |
| `Assets/FPVR/Scripts/TrainingDemo.cs` | Gate course, timer, HUD, propeller spin |
| `Assets/FPVR/Scripts/TrainingGate.cs` | Gate pass detection and highlighting |
| `Assets/FPVR/Scripts/SceneVisuals.cs` | Sky, lighting, post-processing and the grass field (applied at runtime) |
| `Assets/FPVR/Scripts/DroneAudio.cs`, `MotorSynth.cs` | Procedural sound: propellers, wind, coin and jackpot sounds |
| `Assets/FPVR/Scripts/DroneModel.cs` | Swaps the box drone for the imported 3D model |
| `Assets/FPVR/Editor/` | Editor helpers: auto-open the demo scene, Quest VR setup, asset fixes |

## Performance notes

On a standalone Quest, screen-space ambient occlusion and bloom are expensive. If the frame rate drops, untick **Screen Space Ambient Occlusion** in `Assets/FPVR_URP_Renderer.asset`.
