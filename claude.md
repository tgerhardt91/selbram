# Selbram

A 3D marble game built in Godot 4.3 with C#, inspired by Marble Blast Ultra.

## Project Overview

- **Engine:** Godot 4.3 with C# (.NET 6.0)
- **Genre:** 3D marble platformer/racer
- **Multiplayer:** Local splitscreen (not networked)
- **Input:** Controller-first design with keyboard/mouse fallback
- **Platform:** PC (Windows/Mac/Linux)

## Architecture

### Input Abstraction
Input is decoupled from game logic via `IInputProvider` interface. This allows:
- Local input per player (for splitscreen)
- Potential replay/ghost systems later

## Context7 Integration
- **Always use Context7 MCP** when searching library/API documentation, code generation, setup, or configuration steps.
- Do not wait for me to explicitly ask; use the `resolve-library-id` and `get-library-docs` tools automatically when referencing external libraries.
- If a specific version is mentioned (e.g., "Godot 4.3"), ensure Context7 fetches docs for that specific version.
- When looking for Godot documentation use library id /godotengine/godot-docs

Key files:
- `Scripts/Input/IInputProvider.cs` - Interface
- `Scripts/Input/LocalInputProvider.cs` - Reads from Godot input system
- `Scripts/Input/MarbleInput.cs` - Input data struct

### Marble Physics
Uses Godot's `RigidBody3D` with custom force application for rolling feel.

Key tuning parameters (exposed in Inspector):
- `RollForce` - Acceleration strength
- `MaxSpeed` - Velocity cap
- `GroundDeceleration` - How fast marble stops when no input
- `JumpImpulse` - Jump strength
- `AirControlFactor` - Movement control while airborne

Key files:
- `Scripts/Player/MarbleController.cs` - Main physics controller
- `Scripts/Player/MarbleState.cs` - Serializable state (for replays/ghosts)
- `Scripts/Player/MarbleCamera.cs` - Third-person orbit camera

### Surface System
Different surfaces affect marble physics via `SurfaceProperties` resource:
- Friction, bounce, speed modifier, roll resistance
- Presets: Normal, Ice, Mud, Bumper

### Game Flow
- `GameManager` - Singleton managing game state, spawning, timer
- `LevelManager` - Level-specific triggers (finish, checkpoints, kill zones)

## Controls

| Action | Controller | Keyboard |
|--------|-----------|----------|
| Move | Left Stick | WASD |
| Camera | Right Stick | Mouse |
| Jump | A / Cross | Space |
| Power-up | B or RB | E |
| Pause | Start | Escape |
| Respawn | LB | R |

## Project Structure

```
Scenes/
  Marble.tscn        - Marble prefab
  TestLevel.tscn     - Test level with ramps/platforms
Scripts/
  Core/              - GameManager, game state
  Input/             - Input abstraction layer
  Player/            - MarbleController, MarbleCamera, MarbleState
  Level/             - LevelManager, SurfaceProperties
  PowerUps/          - Power-up types and implementations
  UI/                - HUD, menus (not yet implemented)
```

## Current State

Implemented:
- [x] Marble physics with rolling, jumping, deceleration
- [x] Camera system with orbit, smoothing, collision avoidance
- [x] Input abstraction (controller-first)
- [x] Test level with ramps and platforms
- [x] Basic game flow (spawn, timer, finish detection)
- [x] Surface properties system

Not yet implemented:
- [ ] Power-up pickups and effects
- [ ] HUD (timer display, power-up indicator)
- [ ] Local splitscreen multiplayer
- [ ] Multiple levels
- [ ] Menus (main menu, level select, pause)
- [ ] Checkpoints
- [ ] Audio

## Splitscreen Notes

When implementing local splitscreen:
- Each player needs their own `MarbleController` + `MarbleCamera` + `LocalInputProvider`
- Use Godot's `SubViewport` for split rendering
- Input providers should be configured for specific controller device IDs
- Camera collision masks may need adjustment to avoid cross-player issues

## Code Style

- C# with nullable enabled
- Godot naming conventions (`_privateField`, `PublicProperty`)
- Export attributes for Inspector-tunable values
- Region blocks for code organization (`#region Movement`)
