# Monitra.Agent

Windows Service that runs on an employee's workstation, registers itself with the Monitra API
using a tenant admin's install token, and reports basic activity telemetry on a heartbeat.

## What this scaffold does

- Registers once against `POST /api/agent/register` using an install token, persists the
  issued device token locally (DPAPI-encrypted, machine-scoped) at
  `%ProgramData%\Monitra\device.dat`.
- On every heartbeat interval, reads system idle time and the foreground process name via
  Win32 P/Invoke, and posts them to `POST /api/agent/heartbeat` using the `X-Device-Token`
  header.
- Runs as a Windows Service (`Monitra Agent`) via `Microsoft.Extensions.Hosting.WindowsServices`,
  or as a plain console app when launched directly for local development.

## What this scaffold deliberately does NOT do yet

- **No persistence of activity data on the API side.** The heartbeat payload is logged, not
  stored — there is no activity/event data model yet (see project roadmap). Building that
  pipeline, and the reporting/dashboard views on top of it, is the next phase.
- **No MSI installer.** For local testing, run the service directly or `sc create`/`sc start`
  it manually and supply the install token via `Agent:InstallToken` in `appsettings.json` or
  the `MONITRA_INSTALL_TOKEN` environment variable.
- **No offline queueing.** A failed heartbeat is just retried on the next interval; nothing is
  buffered and replayed.
- **No employee-facing notification channel yet** (the SignalR-based push for HR actions
  discussed separately isn't wired up here).
- **Windows only**, by design — idle detection and foreground-window tracking are Win32 APIs
  with no cross-platform equivalent used here.

## Running locally

1. Get an install token for a tenant (e.g. the seeded Acme tenant's token, printed to the
   console by `DbSeeder` on first API run in development).
2. Set `Agent:InstallToken` in `appsettings.json`, or `set MONITRA_INSTALL_TOKEN=...`.
3. Set `Agent:ApiBaseUrl` to your running `Monitra.Api` address.
4. `dotnet run` from this directory. It registers once, then heartbeats on the configured
   interval.
