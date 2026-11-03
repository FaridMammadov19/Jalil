# Jalil

Jalil is a cross-platform Telegram account companion. Configured commands or reactions can archive/restore a customer conversation, or add/remove it from an existing native Telegram folder.

This uses Telegram's user API (MTProto), not BotFather or the Bot API. It runs on macOS and Windows. The packaged versions include .NET, so a recipient does not need developer tools, source code, or a separate .NET installation. The computer running Jalil must be awake, online, and the app must remain running.

## Easiest installation for a non-developer

Give the recipient the ZIP matching their computer:

- `Jalil-osx-arm64.zip` for Apple Silicon Macs (M1, M2, M3, and later).
- `Jalil-osx-x64.zip` for Intel Macs.
- `Jalil-win-x64.zip` for ordinary 64-bit Windows computers.

They extract the ZIP and read `QUICK_START.txt`. On macOS they Control-click **Start Jalil.command** and choose **Open**. On Windows they double-click **Start Jalil.cmd**. The included launcher keeps errors visible instead of immediately closing the window.

The first launch asks locally for the Telegram API ID, API hash, phone number, login code, and optional 2-step verification password. The API values and phone number are remembered in the operating system's user application-data folder; the one-time login code and 2-step password are never saved. Later launches require only a double-click.

## What you need from Telegram

Having the Telegram desktop app installed and signed in is useful, but it does not authorize Jalil by itself.

1. Open [my.telegram.org](https://my.telegram.org) in your browser.
2. Sign in with the same Telegram phone number. Telegram sends the login code to your Telegram account.
3. Open **API development tools**.
4. Create an application. Suggested values:
   - App title: `Jalil`
   - Short name: a unique name such as `jalil-yourname`
   - Platform: `Desktop`
   - Description: `Private work conversation folder automation`
5. Keep the resulting **api_id** and **api_hash** private. Do not paste the API hash, login code, 2-step password, or generated session file into chat or commit them to source control.

On Jalil's first run, enter those values locally. Telegram then sends a login code. If your account uses 2-step verification, Jalil also prompts for that password. The local session and reusable setup values are reused on later runs.

## Configuration

Copy the safe template once, then edit your ignored local `appsettings.json`:

```bash
cp appsettings.example.json appsettings.json
```

The local file is excluded from Git because it may contain your phone number. Its structure is:

```json
{
  "Telegram": {
    "PhoneNumber": "+372..."
  },
  "Routing": {
    "Destination": "Archive",
    "FolderName": "test",
    "AddCommand": "/done",
    "RemoveCommand": "/undo"
  },
  "Behavior": {
    "DeleteCommandMessages": true
  },
  "ReactionRouting": {
    "Enabled": true,
    "PollIntervalSeconds": 5,
    "AddEmoji": "👍",
    "RemoveEmoji": "👎"
  }
}
```

Set `Routing.Destination` to `Archive` for Telegram's native Archive, or `Folder` to use `FolderName`. Folder names support Cyrillic and other Unicode alphabets, including case-insensitive and normalized matching. The two commands must be different. Matching is exact and case-insensitive. When message deletion is enabled, Jalil changes the destination first and then deletes the command message for all participants. If the operation fails, the command remains visible so it can be retried.

When reaction routing is enabled, reacting to the latest message in a conversation with `AddEmoji` archives/adds it, while `RemoveEmoji` restores/removes it. Jalil checks the latest message in the 100 most recent main-list and archived conversations at the configured interval. Only reactions selected by the authorized Jalil account are acted on; other participants' reactions are ignored. Standard emoji reactions are supported in this first version. Commands remain available as a fallback.

The phone number is optional and can remain empty. Do not put the API hash, login code, 2-step verification password, or Telegram session in this file.

## Build handoff ZIPs

Maintainers can create the current Mac package on macOS:

```bash
./scripts/package.sh osx-arm64
```

Use `osx-x64` instead for an Intel Mac. On Windows, run:

```powershell
.\scripts\package.ps1 win-x64
```

Packages are written to the ignored `artifacts` directory. Pushing a version tag such as `v1.0.0` also runs the GitHub workflow, builds all three ZIPs, and attaches them to a GitHub Release. The executables are currently unsigned, so macOS or Windows can show its standard first-launch security confirmation.

## Run on macOS

This repository currently includes a project-local .NET 10 SDK for development on this Mac:

```bash
cd /Users/mjollnnirr/Projects/Bots/Telegram/Jalil
export JALIL_TELEGRAM_API_ID='your-api-id'
export JALIL_TELEGRAM_API_HASH='your-api-hash'
./.dotnet/dotnet run --project src/Jalil.App
```

You may omit the API environment variables and answer the local prompts instead. The API hash and login secrets are hidden while typed. Folder, command, phone, and deletion settings come from `appsettings.json`; command-line and environment folder overrides are still supported.

## Run on Windows

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), open PowerShell in the project directory, then run:

```powershell
$env:JALIL_TELEGRAM_API_ID='your-api-id'
$env:JALIL_TELEGRAM_API_HASH='your-api-hash'
dotnet run --project .\src\Jalil.App
```

The same source is used on both operating systems. A packaged macOS/Windows app and automatic startup are later deployment steps; they are not required for the first integration test.

## Safe first test

List folder names after signing in:

```bash
./.dotnet/dotnet run --project src/Jalil.App -- --list-folders
```

Then test command detection without changing a folder:

```bash
./.dotnet/dotnet run --project src/Jalil.App -- --dry-run
```

Finally run normally and test in a disposable conversation:

1. React to a message with `👍` to archive the conversation.
2. React with `👎` to restore it to the main chat list.
3. Confirm another participant using the same emoji does not trigger an action.
4. Confirm `/done` and `/undo` still work as fallbacks.

Add a fresh reaction after Jalil displays its watching status. If the message already has your chosen reaction, remove it and add it again.

The Telegram session, saved reusable credentials, and update checkpoint are stored outside the repository in the operating system's local application-data directory under `Jalil`. On macOS, the credentials file is owner-readable only. Jalil does not store customer message bodies or media.

## Current scope

- In `Archive` mode, the configured add command/reaction archives the conversation and the remove command/reaction restores it.
- In `Folder` mode, the configured add command/reaction adds the conversation to `FolderName` and the remove command/reaction removes it.
- Incoming commands, suffixed or embedded commands, edits, and media captions are ignored.
- Successfully processed command messages are deleted for all participants when `DeleteCommandMessages` is enabled.
- Configured standard emoji reactions on the latest conversation message can add or remove the conversation; reactions from other participants are ignored.
- Regular existing Telegram folders are supported; imported/shared folders are rejected.
- The first version adds the conversation to the target folder. It preserves other folder memberships.
- Secret chats are not supported.

Stop the process with Ctrl+C.

## Troubleshooting

If startup fails and the short error does not identify the problem, rerun with diagnostics enabled. This prints code locations, but not your configured credentials:

```bash
JALIL_DIAGNOSTICS=1 ./.dotnet/dotnet run --project src/Jalil.App -- --list-folders
```
