# Deploying DemoService under systemd

`demoservice.service` is the unit file for running `demo.hashback.dev` as a
production systemd service, behind the same Cloudflare Tunnel setup used for
ad hoc/debug runs (see the project's Cloudflare Tunnel notes). It uses
`DynamicUser=yes` rather than a dedicated named account - systemd creates and
owns a throwaway, unprivileged UID for the process's whole lifetime, with no
`useradd`/`adduser` step needed.

## 🧰 VM prerequisites

Runtime only, no SDK/compiler:

```bash
# Package name/repo setup varies by distro - see
# https://learn.microsoft.com/en-us/dotnet/core/install/linux
sudo apt install aspnetcore-runtime-10.0
```

Plus `cloudflared`, configured to point its tunnel at `localhost:9001` (the
service's default port - see `--Port` in `DemoService/Program.cs` if that
needs to change).

## 📦 Publishing

Built on a machine with the SDK (this dev machine), not on the VM itself:

```bash
dotnet publish DemoService -c Release -r linux-x64 --self-contained false -o ./publish
```

The explicit `-r linux-x64` matters: it's what makes `dotnet publish` copy in
the native SQLite library (`Microsoft.EntityFrameworkCore.Sqlite` pulls in
`SQLitePCLRaw.bundle_e_sqlite3`, which bundles that library for the target
RID) rather than a bare `dotnet publish`, which may not resolve it. No
separate `apt install sqlite3` is needed on the VM - the native library ships
inside `./publish`.

Copy the contents of `./publish` to `/opt/hashback` on the VM (owned by
`root`, read-only to the service - `DynamicUser`'s `ProtectSystem=strict`
means the service can't write there even if something tried to).

## ⚙️ Installing the unit

```bash
sudo cp demoservice.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now demoservice
sudo systemctl status demoservice
journalctl -u demoservice -f
```

`StateDirectory=hashback` makes systemd create `/var/lib/hashback`, owned by
the service's dynamic UID, and that's where `hashback.db` (see
`ServiceData.DbFilePath`) ends up - `WorkingDirectory` in the unit file
points there for exactly that reason.

## 🔄 Updating an existing deployment

```bash
# From a machine with the SDK, on current main:
dotnet publish DemoService -c Release -r linux-x64 --self-contained false -o ./publish

# On the VM: stop the service *before* copying - see note below.
ssh hugo.vs.mythic-beasts.com "sudo systemctl stop demoservice"
scp -r ./publish/* hugo.vs.mythic-beasts.com:/opt/hashback/
ssh hugo.vs.mythic-beasts.com "sudo systemctl start demoservice"
ssh hugo.vs.mythic-beasts.com "journalctl -u demoservice -f"  # watch the migration apply cleanly
```

Stop first, copy second - not the other way round. Copying the new files
over a *running* process's own binaries (even one already mid-shutdown from
a `systemctl restart`) can make it see a different assembly on disk than
what it loaded into memory, which showed up live as an `Unhandled exception:
System.BadImageFormatException: Index not found` during the old process's
shutdown sequence on 2026-10-06. Harmless in that instance (it was already
on its way out, and the new process started cleanly a second later -
`NRestarts` stayed at 0, nothing actually failed), but stop-then-copy avoids
the noise entirely.

Any pending EF Core migration runs automatically on the next startup,
against whatever's actually in `hashback.db` at that point - no manual DB
step needed, but worth watching the log for it regardless.

## 🚧 Open items

- **`MemoryDenyWriteExecute=yes`**: deliberately left out of the unit file.
  Modern .NET's JIT is generally compatible with it (it toggles a mapping's
  permissions rather than requesting write+execute simultaneously), but this
  has a history of breaking managed runtimes under systemd sandboxing on some
  platform/version combinations, and it isn't worth risking a confusing
  silent startup failure on a box with no interactive debugging set up yet.
  Worth trying as a follow-up hardening step once the service is confirmed
  running normally - if it breaks, the failure should show clearly in
  `journalctl` as the process dying immediately on startup.
- **Account/path names**: `/opt/hashback` and `/var/lib/hashback` (from
  `StateDirectory=hashback`) are just reasonable defaults - rename freely,
  keeping the unit file's `ExecStart`/`WorkingDirectory`/`StateDirectory`
  consistent with wherever things actually end up.
