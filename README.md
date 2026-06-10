# AR Helicopter Mission Map

![Unity](https://img.shields.io/badge/Unity-6.3%20LTS-blue.svg)
![Platform](https://img.shields.io/badge/Platform-Mobile-lightgrey.svg)


## Table of Contents
1. [About the Project](#about-the-project)
2. [Key Features](#key-features)
3. [System Architecture & Workflow](#system-architecture--workflow)
4. [Directory Structure](#directory-structure)
5. [Getting Started](#getting-started)
    * [Prerequisites](#prerequisites)
    * [Installation](#installation)
6. [Usage & Configuration](#usage--configuration)
7. [Mechanics & Input Layout](#mechanics--input-layout)
8. [License & Contact](#license--contact)


---

## About the project

An augmented reality experience built in Unity using Google ARCore. Control a helicopter over a grid mapped onto a physical map using AR markers. Complete missions using a scalable mission system with locations mapped locally using the grid coordinates.

### Built With
* **Core Engine:** Unity 6.3 LTS
* **AR Framework:** AR Foundation & Google ARCore Extensions


##  Key Features

* **Live AR Tracking:** The gamespace is constantly updated upon detecting a marker using ARCore, allowing the helicopter to always stay on the physical map.
* **Local Grid System:** By translating the real-world coordinates into a local space, the mapped locations on the grid will scale with the size of the area between markers without the need of more complex functions.
* **Mission System:** Create missions by dragging pre-set locations in the mission templates to customize it for any area.

##  System Architecture & Workflow

The project is built using a decoupled **3-Layer Architecture** powered by the **Observer Pattern (C# Actions)**. This ensures that physics and tracking calculations are completely separated from the user interface, resulting in smooth performance and easily expandable mission types.

### The 3-Layer Pipeline

```
[1. TRACKING LAYER]         [2. GAMEPLAY LOGIC]         [3. UI / DISPLAY LAYER]
  Real-world ARCore    ➔    MissionStateController  ➔    Decoupled HUD Panels
  Markers & Heli Pos        Calculates Distances        Listen for C# Events
```

---

### Data Flow (Step-by-Step)

1. **The Tracking Layer (`ARCore` & `HelicopterManager`)**
   * ARCore detects physical markers and creates the tracking space.
   * The `HelicopterManager` translates the helicopter's real-world position into a relative, scalable **100x100 grid** mapped to your physical map.

2. **The Gameplay Logic Layer (`MissionStateController`)**
   * The core state machine evaluates a daily `EvaluateProgressionTick()`. 
   * It calculates the flat horizontal distance ($X/Z$ plane) between the helicopter and the active mission destination.
   * If the helicopter enters the `interactionRange`, the controller manages the objective states (e.g., ticking up a timer for `Scan` missions or advancing a step for `Delivery` missions).

3. **The UI Layer (`MissionUIController` & `IntroSequenceController`)**
   * The UI scripts **never** check distances or player data directly. Instead, they simply sit and listen for events.
   * When a gameplay state updates, the logic layer fires a C# Action (like `OnProximityChanged` or `OnScanProgressUpdated`). The UI scripts instantly hear this and trigger panel slides, text changes, or progress bar fills.

---

##  Directory Structure

```bash
Assets/
└──  _Project/
    ├──  Animations/                          # HUD animations like panel extension
    ├──  Images/
    │   └── Waypoint                          # Images to be displayed in waypoints
    ├──  Prefabs/
    ├──  Scenes/
    │   └── MainScene.unity
    └──  Scripts/
        ├──  AR/
        │   └── ARTrackingManager.cs          # Script that handles the marker scanning
        ├──  Editor/
        ├──  Helicopter/
        │   ├── BoundaryManager.cs            
        │   ├── HelciopterManager.cs          # Script that manages the general state of the helicopter
        │   ├── HelicopterMovement.cs
        │   └── RotorControl.cs
        ├──  Intro/
        ├──  Missions/
        │   ├── MarkerManager.cs              # Handles all the spawning of waypoints on the locations
        │   ├── MissionAudioManager.cs
        │   ├── MissionController.cs          # Times all the individual scripts on the right events
        │   ├── MissionData.cs
        │   ├── MissionStateController.cs     # Contains all the missions and mission logic
        │   └── MissionUIManager.cs           # Handles the timing of the UI animations
        ├──  Radar/
        └──  Reset/

```

---

## Getting Started

### Prerequisites
* Unity Hub installed with **Android Build Support** (OpenJDK / Android SDK & NDK Tools) dependencies.
* An Android mobile hardware device officially certified for Google ARCore tracking.
* Physical image **markers** (included in Assets/Markers), optionaly placed on the corners of a map.

### Installation
1. Clone this repository down to your local machine:
   ```bash
   git clone https://github.com/DamianVerkooijen/ARHMM.git
   ```
2. Open Unity Hub, choose **Add project from disk**, and point it to the cloned project directory.
3. Once fully loaded, open the active production scene file: `Assets/_Project/Scenes/MainScene.unity`.
4. Check the Hierarchy if there are no empty Inspector slots before attempting a build to the hardware device.

---

## Usage & Configuration

### Dynamic Mission Structure
Missions are built using simple serializable structures configured directly inside the Unity Inspector layout:

```csharp
[System.Serializable]
public class Mission
{
    public string missionName;
    public MissionType missionType; // Delivery, SearchFind, or Scan
    public bool isCompleted = false;
    
    public MissionTarget startLocation;  // Used by Delivery
    public MissionTarget endLocation;    // Used by Delivery
    
    public List<MissionTarget> searchTargets; // Used by SearchFind
    public List<MissionTarget> scanTargets;   // Used by Scan
}

[System.Serializable]
public class MissionTarget
{
    public string locationName;      // Controlled by custom [LocationName] attribute
    public string actionText;        // Text shown on HUD interaction drawers (e.g., "Scannen...")
    public Sprite targetIcon;        // Specialized task icon pass-through
    public string shortInstruction;  // Brief instruction banner text
    public string description;       // In-depth contextual descriptive prompt
    public int reward;               // Mission progression score metrics
}
```

### Configurable Global Properties
These core fields can be fine-tuned inside the `MissionStateController` component wrapper to match your physical tracking environment scale:

| Variable Identifier | System Type | Default Assignment | Functional Mechanics Under the Hood |
| :--- | :--- | :--- | :--- |
| `missions` | `List<Mission>` | `new List<Mission>()` | Array of all structural narrative missions, objectives, and state trackers. |
| `interactionRange` | `float` | `0.1f` | Target radius for the helicopter to interact with the mission. |
| `scanDuration` | `float` | `2.0f` | Total hover window timeframe required to fill a state check on `Scan` steps. |
| `defaultStartIcon` | `Sprite` | `null` | Fallback sprite assignment when activating a mission. |

---

##  Mechanics & Input Layout
These inputs are the most important for enabling gameplay and managing a session. Resets can be used to recalibrate or to only reset all missions upon completion.

| Input Source / Controller | Game Mechanic | Execution Flow & Behavior |
| :--- | :--- | :--- |
| **Left Joystick** | Throttle & Strafe| Forward/Backward and strafing movement for the helicopter object. |
| **Right Joystick** | Yaw | Rotation of the helicopter, important for heading-based movement. |
| **Start Button Click** | Enables Gameplay | Loads in all missing UI elements, fades away the explanation pop-up and enables joystick input. |
| **Mission Reset Click** | Mission Loop Reset | Stops current mission routine, recenters the helicopter in the game space and enables the intro pop-up again. |
| **Full Reset Click** | Full Scene Reset | Reloads the scene, requiring the markers to be scanned again before continuing the gameplay |

---

##  License & Contact

Distributed under the MIT License. See project root `LICENSE` asset parameters for more details.

* **Project Developer:** DamianVerkooijen
* **Repository Endpoint:** https://github.com/DamianVerkooijen/ARHMM
