# 🎮 Platform Survival 3D (Android & Unity)

A fast-paced 3D multiplayer survival platformer inspired by *Fall Race 3D* and *Hex-A-Gone* built with **Unity 6 / Unity 2021.3** and C#.

---

## 📸 Key Gameplay Features

- **🏢 Multi-Layer 3-Floor Hexagon Arena**:
  - 3 vertical floors of procedural 3D hexagonal tiles with distinct color tiers (Layer 1: Blue/Cyan, Layer 2: Gold/Yellow, Layer 3: Pink/Magenta).
  - Tiles detect player/bot contact, shake with warning color transitions, and drop with gravity.
  - Players drop down through floor holes to continue fighting on lower layers.
  - Falling below the bottom-most floor eliminates the participant.

- **🦘 Smart Automatic Jump System (No Jump Button)**:
  - Downward & forward raycast ground probes detect edge gaps and falling platforms in the direction of movement.
  - Automatically launches the character into a physics-driven forward leap to cross gaps cleanly.
  - Responsive air control allows steering toward adjacent tiles mid-jump.

- **🤖 Autonomous AI Bots (4 Opponents)**:
  - Continuously evaluates tile stability underfoot.
  - Steers away from shaking/falling tiles toward stable platforms on the current floor.
  - Uses the same auto-jump mechanic across gaps.
  - Fair randomized human-like reaction latency.

- **🕹️ Smooth On-Screen Joystick**:
  - Touch-friendly virtual joystick for Android mobile devices.
  - Automatic fallback to **WASD** / **Arrow Keys** in Unity Editor.

- **✨ Visual Polish & Juice**:
  - Procedural character squash & stretch on jump launch and landing.
  - Fall Guys style bean characters with cute faceplates/visors.
  - Dynamic camera with mobile portrait framing, smooth floor glide, and void fall freeze.
  - Real-time alive counter HUD (`ALIVE: X / 5`).
  - Victory & Game Over screens with placement ranks and Retry / Main Menu navigation.

---

## 🗂️ Project Structure

```text
Assets/
├── Scenes/
│   ├── SplashScene.unity     # Title screen, tutorial guide & Play button (Build Index 0)
│   ├── GameplayScene.unity   # Main 3-floor hexagon survival match (Build Index 1)
│   └── SampleScene.unity     # Duplicate gameplay scene for convenience
├── Scripts/
│   ├── AI/
│   │   └── BotController.cs               # AI bot pathfinding, tile query & jumping
│   ├── Camera/
│   │   └── FollowCamera.cs                # Smooth 3rd person chase camera & void clamp
│   ├── Core/
│   │   └── GameManager.cs                 # Game state, match loop, spawning & rules
│   ├── Editor/
│   │   ├── BuildScript.cs                 # Android APK batchmode & menu builder
│   │   └── SceneSetupUtility.cs           # Automated scene generation & wiring
│   ├── Platform/
│   │   ├── PlatformGridGenerator.cs       # Procedural 3D hexagon multi-floor arena
│   │   └── PlatformTile.cs                # Tile contact, wobble, color fade & drop
│   ├── Player/
│   │   ├── CharacterSquashAndStretch.cs   # Procedural jump/land squashing
│   │   └── PlayerController.cs            # Rigidbody movement & forward auto-jump
│   └── UI/
│       ├── SplashManager.cs               # Main menu UI logic
│       ├── UIManager.cs                   # HUD, alive counter, Victory/GameOver panels
│       └── VirtualJoystick.cs             # On-screen mobile touch joystick
└── Builds/
    └── PlatformSurvival.apk               # Ready-to-install Android APK (~26.8 MB)
```

---

## 🚀 How to Run in Unity

1. Open the project in **Unity 6** (or **Unity 2021.3+**).
2. In the Project window, navigate to `Assets/Scenes/`.
3. Open **`SplashScene.unity`** (or **`GameplayScene.unity`**).
4. Click **Play ▶️** in the Editor.
   - Use **WASD** / **Arrow Keys** or drag the **On-Screen Joystick** with your mouse to move.
   - Stepping on tiles starts their fall sequence.
   - Move toward gap edges to auto-jump across.

---

## 📱 Android APK Build

- A compiled, playable Android APK is available in:
  ```
  Builds/PlatformSurvival.apk
  ```
- To rebuild the APK inside Unity:
  - Click menu bar: **`Build`** $\rightarrow$ **`Build Android APK`**.
  - Or via command line batchmode:
    ```bash
    Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildAndroid -logFile build.log
    ```

---

## 📜 Submission Checklist
- [x] Multi-layer platform grid
- [x] On-screen touch joystick (free 360° movement)
- [x] Automatic jump without jump button
- [x] Falling platform mechanics with visual feedback
- [x] 3–5 AI bots with falling tile avoidance & auto-jump
- [x] Player elimination & last player standing victory
- [x] Remaining players counter, Game Over, Victory, Retry & Main Menu navigation
- [x] Clean Git repository with `.gitignore` (excludes `Library/`, `Temp/`, etc.)
- [x] Playable Android APK
