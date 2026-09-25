# SoapSlide

**A last-one-standing party game for up to 8 players on a slippery arena, made in Unity 6.** Every round you get a few seconds to aim and pick a slide force. Then everyone lets go at once, bodies collide, and whoever slides off the edge is out.

This repository holds both stages of the project in its history:

| Commit | Stage | What it is |
|---|---|---|
| `75b8ec8` (Apr 2026) | **Offline prototype** | You vs 7 bots, free-for-all or 4v4 teams, spectator camera |
| `3d31dcc` (Jun 2026) | **Online version** | The same game made online: server-authoritative netcode, matchmaking, dedicated servers, bots filling empty slots. The offline mode is kept. |

---

## Gameplay

- **Plan:** each round has a short planning phase (8 seconds). Aim with the planning arrow and choose a force from 1 to 10.
- **Slide:** everyone launches at the same time. Physics resolves the collisions on the soapy arena.
- **Survive:** falling off eliminates you. The last player, or last team, standing wins.
- **Modes:** free-for-all, or 4v4 teams. **Bots** fill empty slots and plan their own slides.
- **Spectate:** eliminated players follow the rest of the match with a spectator camera.

**Status:** networked playable prototype. A Linux dedicated-server build (for Edgegap) and a Windows client build were produced, but end-to-end online play hasn't been verified yet.

## Tech stack

| Area | Offline prototype | Online version |
|---|---|---|
| Engine | Unity 6 (6000.0.32f1), Built-in RP + Post Processing v2 | Unity 6 (6000.0.32f1), **URP** 17 |
| Networking | – | **Netcode for GameObjects** 2.11. Server-authoritative: clients send their slide plan by RPC, the server simulates the physics, and `NetworkTransform` / `NetworkRigidbody` replicate the result. |
| Services | – | **Unity Authentication** (anonymous), **Lobby**, **Relay**, **Matchmaker** → **Edgegap** dedicated servers through a **Cloud Code** allocator, **Vivox** voice, **Friends**, **Cloud Save** |
| Framework | – | **Multiplayer Engine Pro** (Ignitive Labs, Asset Store) for the lobby → matchmaking → game flow |
| Input | Unity Input System | Unity Input System |
| UI | Built in code | Built in code, plus the engine's lobby UI |

## What I built

The game code lives in **`Assets/Scripts/SoapSlide/`**.

| System | Key scripts |
|---|---|
| Round flow (planning → sliding → resolution → elimination) | `SoapSlideRoundDirector.cs`, `ISoapSlideMatchDirector.cs`, `SoapSlideSlotPlan.cs`, `SoapSlideFallZone.cs` |
| Slide physics and bot AI | `SlideParticipant.cs`, `SlidePlanningArrow.cs` |
| Arena and match setup: builds the arena in code; server, client and offline startup paths; bot backfill | `SoapSlideGameBootstrap.cs` |
| Networking (online version) | `Networking/` (for example `SoapSlidePlayerNet`, `SoapSlideGameStateNetwork`, `SoapSlideClientDirectorView`, `SoapSlideOnlineHudContext`) |
| Camera, spectating and visuals | `SoapSlideCameraFollow.cs`, `SoapSlideSpectatorCameraDriver.cs`, `SoapSlideLocalPlayerVisuals.cs`, `SoapSlideVisualUtil.cs` |
| Input, HUD and menus | `SoapSlideInput.cs`, `SoapSlideHUD.cs`, `SoapSlideMenuReturn.cs`, `Lobby/` (menu bootstrap, lobby session, bot name generator) |
| Dedicated-server allocation (C# Cloud Code module) | `CloudCode/EdgegapAllocator/` |

**Changes I made to Multiplayer Engine Pro** to plug the game in:
- a new `AnonymousAuthManager`
- a SoapSlide game mode and a `GameSceneName` lobby key
- a fixed matchmaking queue
- deferred server scene loading
- a late-join fix in `MatchmakingSessionManager`
- a spawn RPC fix in `SpawnManagerBase`

### Code highlights

- **`SoapSlideGameBootstrap.cs`:** one bootstrap for three roles: dedicated server, client and offline. It backfills empty player slots with bots, so a match always has 8 participants.
- **`SlideParticipant.cs`:** turns a planned direction and force into a physics impulse. The same code drives humans and bots.
- **`Networking/`:** the plan-then-simulate model keeps network traffic tiny. Only each player's plan goes up; the server's physics result comes back.
- **`CloudCode/EdgegapAllocator/`:** Cloud Code module that asks Edgegap for a dedicated server when the matchmaker forms a match. The API token comes from Unity's Secret Manager and is never stored in the repo.

## Scenes

| Scene | Purpose |
|---|---|
| `Assets/Scenes/Menu.unity` | Main menu, offline play |
| `Assets/MultiplayerEngine/Scenes/MatchMaking/*` | Bootstrap → casual lobby → matchmaking → game (online) |

The build list also contains Multiplayer Engine Pro's P2P and persistent-server sample scenes.

## Integrated third-party assets

| Asset | Used for |
|---|---|
| Multiplayer Engine Pro (Ignitive Labs) | Online framework: auth, lobby, matchmaking, dedicated-server flow, UI |
| Unity Dedicated Server "barebones" sample | Leftover server sample scaffolding |
| TextMesh Pro | UI text |

## About this repository

This public repository is a **showcase**. It contains the documentation and the **31 source files I wrote** for this project. The complete project, including licensed third-party assets that cannot be redistributed, is kept in a private repository.

Copyright © Omer Avcioglu (McHunter Studio). **All rights reserved.** Viewing only; see LICENSE.
