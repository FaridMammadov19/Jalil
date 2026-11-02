# Jalil

Jalil is a cross-platform Telegram account companion. When you send the exact outgoing message `/done` in a customer conversation, Jalil adds that conversation to an existing native Telegram folder. Send `/undo` to remove it from that folder.

This uses Telegram's user API (MTProto), not BotFather or the Bot API. It runs on macOS and Windows. While you are testing locally, the computer running Jalil must be awake, online, and the app must be running.

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

On Jalil's first run, enter those values locally. Telegram then sends a login code. If your account uses 2-step verification, Jalil also prompts for that password. The local session is reused on later runs.

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
    "FolderName": "test",
    "AddCommand": "/done",
    "RemoveCommand": "/undo"
  },
  "Behavior": {
    "DeleteCommandMessages": true
  },
  "ReactionRouting": {
    "Enabled": true,
    "AddEmoji": "👍",
    "RemoveEmoji": "👎"
  }
}
```

The two commands must be different. Matching is exact and case-insensitive. When message deletion is enabled, Jalil changes the folder first and then deletes the command message for all participants. If the folder operation fails, the command remains visible so it can be retried.

When reaction routing is enabled, reacting to any message with `AddEmoji` adds its conversation to the folder, while `RemoveEmoji` removes it. Only reactions selected by the authorized Jalil account are acted on; other participants' reactions are ignored. Standard emoji reactions are supported in this first version. Commands remain available as a fallback.

The phone number is optional and can remain empty. Do not put the API hash, login code, 2-step verification password, or Telegram session in this file.

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

1. React to a message with `👍` to add the conversation.
2. React with `👎` to remove it again.
3. Confirm another participant using the same emoji does not trigger an action.
4. Confirm `/done` and `/undo` still work as fallbacks.

Add a fresh reaction after Jalil displays its watching status. If the message already has your chosen reaction, remove it and add it again.

The Telegram session and update checkpoint are stored outside the repository in the operating system's local application-data directory under `Jalil`. Jalil does not store customer message bodies or media.

## Current scope

- The configured add command adds the conversation; the configured remove command removes it. Surrounding whitespace and letter case are ignored.
- Incoming commands, suffixed or embedded commands, edits, and media captions are ignored.
- Successfully processed command messages are deleted for all participants when `DeleteCommandMessages` is enabled.
- Configured standard emoji reactions can add or remove the conversation; reactions from other participants are ignored.
- Regular existing Telegram folders are supported; imported/shared folders are rejected.
- The first version adds the conversation to the target folder. It preserves other folder memberships.
- Secret chats are not supported.

Stop the process with Ctrl+C.

## Troubleshooting

If startup fails and the short error does not identify the problem, rerun with diagnostics enabled. This prints code locations, but not your configured credentials:

```bash
JALIL_DIAGNOSTICS=1 ./.dotnet/dotnet run --project src/Jalil.App -- --list-folders
```
