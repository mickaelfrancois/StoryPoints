# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

StoryPoints is a real-time planning-poker (story-point estimation) web app built with **Blazor Server / Interactive Server** on **.NET 9**. Users create a "salon" (room), share its URL, and vote together on a chosen scale. UI text is in French; code is in English. UI styling is Bootstrap.

## Commands

Run all commands from the repository root (the directory containing `StoryPoints.csproj`).

```powershell
dotnet build                 # build
dotnet run                   # run with default (http) profile -> http://localhost:5134
dotnet run --launch-profile https   # https -> https://localhost:7014
dotnet watch                 # run with hot reload
```

There is no test project and no migrations: the SQLite schema is created at startup via `db.Database.EnsureCreated()` (see `Program.cs`). The database file `StoryPoints.db` is created in the content root.

Docker: the `Dockerfile` targets Linux and exposes 8080/8081; it is wired for the Visual Studio container debug profile.

## Architecture

The central design decision is a **split between durable and live state**:

- **Durable (SQLite via EF Core)** — `Data/AppDbContext.cs`, `Data/Room.cs`. A `Room` row holds only configuration and lifecycle metadata: chosen `Scale`, optional `MaxVoteDurationSeconds`, `CreatedAt`, `LastActivityUtc`. EF is used through `IDbContextFactory<AppDbContext>` (not a scoped `DbContext`), because Blazor Server components are long-lived.
- **Live / ephemeral (in-memory singletons)** — `Services/RoomCoordinator.cs`. The actual session state (who joined, who voted, the current vote values, AFK set, round number, countdown/deadline timers) lives in `RoomState` objects held by the `RoomCoordinator` singleton. **This state is never persisted and is lost on restart.** Only metadata survives in SQLite.

A `Room` row can exist without a live `RoomState` (e.g. after restart); `RoomCoordinator.GetOrCreate` lazily rebuilds the `RoomState` from the stored `Room` config when someone navigates to the room.

### Reveal logic (`RoomState`)

`RoomState` is the concurrency-sensitive core; all mutations happen under a single `_lock`. Votes reveal when:
1. **Everyone (non-AFK) has voted** → immediate reveal, or
2. **≥ 2/3 of non-AFK members voted** → a 10-second countdown starts (`CountdownDeadlineUtc`); if the threshold later drops, the countdown is cancelled, or
3. **The optional per-round vote deadline elapses** (`VoteDeadlineUtc`, started on the first vote of the round when the room has a `MaxVoteDuration`).

Constants `CountdownSeconds = 10` and `RevealThreshold = 2/3` are defined in `RoomCoordinator`. The countdown and deadline each run on their own `CancellationTokenSource`; cancellation is always done via the `...Locked` helpers while holding `_lock`.

### Live updates to clients

Components subscribe to `RoomState.Changed` (an `Action` event) and call `InvokeAsync(StateHasChanged)` to re-render. There is no SignalR hub code — Blazor Server's circuit handles the push. `RoomPage.razor` additionally runs a 250 ms `System.Threading.Timer` purely to animate countdown/deadline displays, started/stopped by `UpdateCountdownTimer`.

### Activity tracking & cleanup (decoupled by design)

To avoid a DB write on every vote/join, `RoomState.Changed` only stamps an in-memory `_pendingActivity[roomId] = UtcNow` in the coordinator. `Services/CleanupService.cs` (a `BackgroundService`) periodically:
1. **Drains** buffered activity (`RoomCoordinator.DrainActivity`) and flushes `LastActivityUtc` to SQLite, then
2. **Deletes** rooms inactive beyond `Cleanup:InactiveDays` and **evicts** their `RoomState` from the coordinator.

It also runs one final drain/cleanup cycle on shutdown. Interval is `Cleanup:RunIntervalHours`.

### Room creation limits

`Services/RoomCreationGuard.cs` (singleton) enforces a per-IP hourly rate limit and a global room cap (`RoomLimits` config) before a room is inserted in `Home.razor`. The client key is the remote IP, captured in `OnInitialized` and stabilized across prerender/interactive via `PersistentComponentState`.

### Identity

There is no authentication. A member is identified by a client-generated `Guid` (per browser/circuit). The display name is persisted in the browser via `ProtectedLocalStorage` (key `memberName`) so it pre-fills on return. Leaving a room (component `DisposeAsync`) calls `RoomState.Leave`.

### Scales

`Data/Scale.cs` defines the enum (`Fibonacci`, `FibonacciShort`, `TShirt`) and `ScaleProvider` maps each to its card set and label. Non-numeric cards (`?`, `☕`, T-shirt sizes) are excluded from min/median/max math by `Services/ResultCalculator.cs` and reported separately as a `SpecialBreakdown` count. Median is snapped to the nearest card on the scale.

## Configuration

`appsettings.json` keys (bound via `IOptionsMonitor` so they're hot-reloadable):
- `Cleanup:InactiveDays`, `Cleanup:RunIntervalHours`
- `RoomLimits:MaxTotalRooms`, `RoomLimits:MaxCreationsPerHourPerIp`

Defaults in the options classes differ from `appsettings.json`; the JSON values win at runtime.

## Conventions

- Keep all session/live mutation logic inside `RoomState` under its lock — components should call `RoomState` methods, never reach into its collections.
- After mutating `RoomState` from a component, call `RefreshLocal()` to re-pull `Snapshot()` / `GetMyVote()` into local fields.
- Use `IDbContextFactory` and `await using` per operation; do not hold a `DbContext` across awaits in a component.
