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

## 🚧 Open items

- **NAT64/IPv4 on the VM**: pending confirmation of how the VM's networking
  actually works - may need a small change to how outbound DNS resolution is
  handled once that's known. Nothing to do here yet.
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
