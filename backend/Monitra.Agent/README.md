# Monitra.Agent

A per-user Windows tray application that runs on an employee's workstation, registers itself
with the Monitra API using a tenant admin's install token, tracks work-hour activity, and
prompts the employee directly when it needs something from them (an inactivity reason, a break
request).

## v0.2 architecture correction: no longer a Windows Service

This was originally scaffolded as a Windows Service. Services run in an isolated system session
(Session 0) and **cannot show UI to a logged-in user** - so once the product needed to prompt
the employee interactively, that model stopped working. This is now a normal per-user
application (`OutputType=WinExe`, `UseWindowsForms=true`) that should be configured to start at
user login (Task Scheduler "at logon" trigger, or a Startup shortcut - the MSI installer that
would set this up doesn't exist yet, see below).

Background telemetry logic (idle detection, foreground-app tracking, heartbeat) is unchanged
from v0.1; what changed is the hosting model, plus everything needed to actually show a prompt.

## What it does

- **Registers once** against `POST /api/agent/register` using an install token; persists the
  issued device token DPAPI-encrypted (`DataProtectionScope.CurrentUser`) at
  `%LOCALAPPDATA%\Monitra\device.dat`.
- **Heartbeats** on an interval with idle time and foreground process name.
- **Caches the tenant's policy** (idle threshold, breaks/day, break duration, tracking window)
  locally at `%LOCALAPPDATA%\Monitra\policy.json`, refreshed periodically, so break/inactivity
  logic still works if the API is briefly unreachable. The API remains the source of truth -
  the cache only avoids a round trip on every check.
- **Detects inactivity locally**: when idle time crosses the cached threshold during the
  tracking window, shows an interactive prompt (`Tray/InactivityPromptForm`) asking for a
  reason, then reports the result to `POST /api/agent/inactivity`.
- **Break requests**: a tray menu item calls `POST /api/agent/breaks`; the server evaluates the
  quota (source of truth) and the agent suppresses inactivity prompts for the approved window.
- **Device health**: samples CPU/RAM/disk/battery and top memory-consuming processes
  (`Services/DeviceHealthService`), reports to `POST /api/agent/health` on an interval.
- **Logs**: buffers its own operational log lines and flushes them to `POST /api/agent/logs`.

## What this still does NOT do

- **No MSI installer**, and therefore no "start at login" configuration out of the box - for
  local testing, run it directly or add a Task Scheduler "at logon" trigger yourself.
- **No code signing** - required before real distribution; unsigned tray apps that watch
  foreground windows will get flagged by AV/EDR/SmartScreen.
- **No offline event queueing beyond logs** - a failed heartbeat/health/inactivity call is
  logged and retried next tick, not persisted and replayed.
- **Tracking-window check uses machine local time**, not the tenant's configured `Timezone` -
  fine for a single-region pilot, not for a distributed team across time zones yet.
- **CPU-per-process and precise CPU% are approximated** (see `DeviceHealthService` comments) -
  good enough for a fleet-health gauge, not a profiler.
- **No HR-issued notification push (SignalR) yet** - that's the separate `EmployeeAction` /
  notification-layer work, still Phase 4.

## Running locally

1. Get an install token for a tenant (the seeded Acme tenant's token is printed to the console
   by `DbSeeder` on first API run in development).
2. Set `Agent:InstallToken` in `appsettings.json`, or `set MONITRA_INSTALL_TOKEN=...`.
3. Set `Agent:ApiBaseUrl` to your running `Monitra.Api` address.
4. `dotnet run` from this directory (requires Windows - this project targets
   `net10.0-windows` and won't build on other platforms). It registers once, shows a tray icon,
   and starts heartbeating/checking activity on the configured intervals.
