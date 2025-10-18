# System Architecture Documentation
## AGT System Reconstruction - Body Tracking & Gesture Recognition

**Version**: 1.0 | **Date**: October 18, 2025 | **Reading Time**: ~5 minutes

---

## 📋 Executive Summary

A **gesture-based interaction framework** for Unity that receives body tracking data from TouchDesigner via OSC protocol. Built with industry-standard design patterns for modularity and maintainability.

**Key Features:** Real-time body tracking (33 landmarks) • Arm direction vectors • 8-direction gesture recognition • Bidirectional OSC communication • State-based game flow

**Tech Stack:** Unity 2022.3+ | uOSC | MediaPipe | C#

---

## 🏗️ System Architecture

### Layer Diagram

```
┌─────────────────────────────────────────────────────────┐
│              TOUCHDESIGNER (External)                   │
│         Camera → MediaPipe → Body Tracking              │
└────────────────────┬────────────────────────────────────┘
                     │ OSC UDP
                     │ Port 3333 ↓  Port 8000 ↑
┌────────────────────┴────────────────────────────────────┐
│                  UNITY APPLICATION                       │
│                                                          │
│  ┌────────────────────────────────────────────────┐    │
│  │   INPUT LAYER (Anti-Corruption)                │    │
│  │   • InputFacade: Parse OSC → Pose data         │    │
│  │   • Transform coordinates                      │    │
│  └──────────────────┬─────────────────────────────┘    │
│                     │ OnPoseDataReceived                │
│  ┌──────────────────┴─────────────────────────────┐    │
│  │   PROCESSING LAYER                             │    │
│  │   • SimpleInteractionBridge: Pose → Events     │    │
│  │   • CoordinateConverter: Coord transforms      │    │
│  │   • HandVisualizer: Visual feedback            │    │
│  └──────────────────┬─────────────────────────────┘    │
│                     │ OnHandInteraction                 │
│  ┌──────────────────┴─────────────────────────────┐    │
│  │   GAME FLOW LAYER (State Pattern)              │    │
│  │   • GameManager: State machine                 │    │
│  │   • States: MainMenu, Tutorial1, Tutorial2     │    │
│  │   • UIManager: UI control                      │    │
│  └──────────────────┬─────────────────────────────┘    │
│                     │ Game Events                       │
│  ┌──────────────────┴─────────────────────────────┐    │
│  │   OUTPUT LAYER                                  │    │
│  │   • OutputFacade: Send OSC messages            │    │
│  └────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────┘
```

---

## 🎯 Core Components

### 1. Input/Output Facades (Anti-Corruption Layer)

#### **InputFacade.cs**
```
Purpose: Single entry point for body tracking data
         
OSC → Parse [index,x,y,z] → Transform coords → Cache pose → Broadcast event
```

**Key:** Port 3333 | Address: `/mediapipe/pose/world` | Event: `OnPoseDataReceived`

#### **OutputFacade.cs**
```
Purpose: Single exit point for game messages

Game Event → Format OSC → Send to TD (Port 8000)
```

**Methods:** `SendGameState()` | `SendAudioEvent()` | `SendHandData()` | `SendCustomTrigger()`

---

### 2. Processing Layer 

#### **SimpleInteractionBridge.cs (to be removed)(TODO: Eventbus)**
```
Pose Data → Calculate confidence → World→Screen coords → Broadcast hand events
```

**Output:** `HandInteractionData { position, fingerCount, confidence, isValid }`

#### **CoordinateConverter.cs** (Static Utility)
```
Centralized coordinate transformations:

TouchDesigner (normalized) 
  → World Space 
  → Screen Pixels 
  → UI Local Coords
```

#### **HandVisualizer.cs**
```
Visual debugging: 6 landmarks + 2 arm direction vectors

Landmarks: Shoulders (blue), Elbows (yellow), Wrists (green)
Vectors: Left arm (cyan), Right arm (magenta)
```

---

### 3. Game Flow (State Pattern)

#### **GameManager.cs**
```
State machine managing game flow:

MainMenu ──[Start Tutorial]──> Tutorial1 ──[Complete]──> Tutorial2 ──[Complete]──> MainMenu
   └──────[Start Game]──────> Gameplay ──[Game Over]──> Results ──────────────────┘
```

#### **Tutorial1State.cs** - 8-Direction Test
```
Test 8 arm directions (4 per side):

LEFT ARM:                  RIGHT ARM:
1. Top (↑)                 5. Top (↑)
2. Right-Top (↗)           6. Left-Top (↖)
3. Right (→)               7. Left (←)
4. Right-Bottom (↘)        8. Left-Bottom (↙)

Algorithm:
  armDirection = (shoulder→elbow + shoulder→wrist) / 2
  angle = Atan2(-direction.y, direction.x)  // Y inverted
  map to 8 zones (45° each)
  require 2-second hold
```

#### **Tutorial2State.cs** - Hands Over Head
```
Detect: leftWrist.y > nose.y && rightWrist.y > nose.y
Hold: 2 seconds → Return to MainMenu
```

---

## 📊 Data Flow Diagram

### Incoming Data (TouchDesigner → Unity)

```
TouchDesigner
    ↓ OSC: /mediapipe/pose/world [0,x,y,z, 11,x,y,z, ...]
InputFacade
    ↓ Parse & Transform
Pose Object (33 landmarks max)
    ↓ OnPoseDataReceived event
    ├─> SimpleInteractionBridge → OnHandInteraction
    ├─> HandVisualizer → Update visuals
    └─> Tutorial States → Check gestures
```

### Outgoing Data (Unity → TouchDesigner)

```
Game Event (state change, gesture complete)
    ↓
OutputFacade.SendX()
    ↓ Format OSC message
TouchDesigner (Port 8000)
    ↓ Trigger visual
```

---

## 🎨 Design Patterns

### Facade Pattern
```
InputFacade/OutputFacade hide OSC complexity

BAD:  Game Logic ──> uOSC Library (complex parsing, error handling)
GOOD: Game Logic ──> Facade ──> uOSC Library
                     (simple API)
```

### State Pattern
```
Each game phase = separate class implementing IGameState

interface IGameState {
    Enter(GameManager)
    Update()
    Exit()
}

Benefits: Isolated logic, easy to add states, clear transitions
```

### Singleton Pattern
```
Global access to key systems:
  InputFacade.Instance
  OutputFacade.Instance
  GameManager.Instance
  UIManager.Instance
  DebugLogger.Instance
```

### Observer Pattern (Events)
```
Publisher ──[event]──> Multiple Subscribers

InputFacade.OnPoseDataReceived ──> Bridge, Visualizer, States
Bridge.OnHandInteraction ──> Visualizer, UIOverlap, States
```

---

## 🗺️ Coordinate System

```
1. TouchDesigner Normalized
   X: -0.5 to 0.5 | Y: -0.9 to -0.3 [INVERTED!]
   
2. Unity World Space
   X: Left(-) to Right(+) | Y: Down(-) to Up(+)
   Scale: (1920, 1080, 1)
   
3. Unity Screen Pixels
   Origin: Bottom-left | X: 0 to width | Y: 0 to height
   
4. Unity UI Local
   Origin: Canvas center
```

**Y-Axis Fix:** `adjustedY = -direction.y` in Tutorial1State

---

## 🔧 Configuration

```csharp
// OSC
InputFacade:  Port 3333, Address "/mediapipe/pose/world"
OutputFacade: Port 8000, Target "127.0.0.1"

// Thresholds
handConfidenceThreshold: 0.5f
poseChangeThreshold: 0.01f
holdDuration: 2.0f seconds
directionCheckInterval: 0.1f seconds
```

---

## 📁 File Structure

```
Scripts/
├── InputFacade.cs              # Input gateway
├── OutputFacade.cs             # Output gateway
├── SimpleInteractionBridge.cs  # Pose → Hand events
├── CoordinateConverter.cs      # Coordinate utilities
├── HandVisualizer.cs           # Visual debugging
├── GameManager.cs              # State machine
├── UIManager.cs                # UI management
├── DebugLogger.cs              # Logging (debug_log.txt)
├── DataModel/
│   ├── Pose.cs                 # 33 landmarks
│   └── BodyLandmark.cs         # Landmark enum
└── States/
    ├── MainMenuState.cs
    ├── Tutorial1State.cs       # 8-direction test
    └── Tutorial2State.cs       # Hands up test
```

---

## 📡 OSC Messages

### Incoming (TD → Unity)
```
/body_pose: [index, x, y, z, index, x, y, z, ...]
Example: [0, 0.1, -0.5, 0.0, 11, -0.2, -0.4, 0.0, ...]
```

### Outgoing (Unity → TD)
```
/game/state:   [stateName, action]
/game/audio:   [eventName, intensity]
/game/hand:    [x, y, fingers, confidence]
/game/trigger: [triggerName, value]
```

---

## 🔍 Debug Tools

```
DebugLogger:     debug_log.txt (project root, rewrites each play)
HandVisualizer:  Visual feedback (landmarks + vectors)
Debug Keys:      1-5 (state transitions), N (skip), Escape (menu)
```

---

### Data Flow Rules
- All external data enters via **InputFacade**
- All outgoing messages via **OutputFacade**
- All coordinate transforms via **CoordinateConverter**
- All logging via **DebugLogger**
- All state transitions via **GameManager**

---

**End of Documentation**

