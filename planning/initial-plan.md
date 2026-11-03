# Jalil: Initial Plan

- **Status:** Implementation in progress
- **Date:** 2026-11-02
- **Outcome:** Configured outgoing commands or the authorized user's configured standard emoji reactions archive/restore a customer conversation, with custom-folder routing retained as a configurable mode.
- **Product shape:** A single-user Telegram client companion using the user API (MTProto), not a Bot API bot.

## 1. Current decision

Proceed with the native-folder experience using an explicit outgoing command instead of reactions.

The companion will:

1. Authorize the user's Telegram account.
2. Observe an outgoing `/done` message sent from another Telegram session, such as the official mobile or desktop app.
3. Resolve the command message's containing chat.
4. Map `/done` to an existing native folder.
5. Add the chat to that folder without overwriting unrelated folder settings.

Initial assumptions:

- One Telegram account and one companion instance.
- The first supported trigger is the exact, case-insensitive command `/done`, with surrounding whitespace ignored.
- The command is intentionally visible to the customer; automatic deletion is deferred.
- The user must be able to send messages in the conversation. Read-only chats and channels are out of scope.
- Target folders already exist.
- “Move” means add the chat to the target folder and remove it only from other folders managed by Jalil. Unmanaged folder membership is preserved.
- Secret chats are excluded because they are device-specific.

## 2. Integration proof

Outgoing messages have a direct Telegram update containing the message and peer identity, so `/done` removes the reaction-specific uncertainty. The first milestone still needs to prove the complete cross-session path before configuration, persistence, deployment, or a UI is built.

Create a disposable local probe that records sanitized MTProto update shapes while the user sends `/done` in an official Telegram app.

### Required experiments

Run each test with the companion connected and the command sent in a different official Telegram session:

| Scenario | Required observation |
| --- | --- |
| `/done` in a customer private chat | Outgoing flag, chat ID, message ID, and exact text |
| `/done` in a basic group | Same identifiers and text |
| `/done` in a supergroup | Same identifiers, text, and topic/thread metadata if present |
| `/done` sent from Telegram mobile | Companion receives and correlates the outgoing message |
| `/done` sent from Telegram desktop | Companion receives and correlates the outgoing message |
| Incoming `/done` sent by another participant | It is ignored |
| `/done now`, embedded text, and media captions | They are ignored in the first version |
| `/DONE` and surrounding whitespace | They are accepted after normalization |
| Duplicate/replayed update | It produces at most one folder mutation |
| Companion disconnected and restarted | The command is recovered and processed once, or the limitation is recorded |

The probe must also verify that it can:

1. Read existing dialog filters.
2. Find an existing folder by a normalized exact name.
3. Add a disposable test chat to the folder.
4. Preserve the folder title, icon, flags, pinned chats, include list, and exclude list.
5. Observe the change in official Telegram clients.

### Success rule

- The companion receives the outgoing `/done`, identifies the correct chat, and moves it to the configured folder.
- Incoming or non-exact commands cannot trigger a move.
- A repeated update is idempotent.
- The path works without scanning chat history or message contents.
- If offline recovery is incomplete, live processing may proceed only after the limitation and user-visible behavior are documented.

## 3. Proposed technical baseline

- **Runtime:** .NET 10 LTS.
- **Prototype form:** Console application running locally on macOS.
- **Telegram integration:** WTelegramClient for the initial MTProto integration. It is pure C# and exposes update events and the complete Telegram Client API.
- **Production client decision:** Re-evaluate WTelegramClient versus official TDLib after the integration proof. TDLib provides stronger client-state and update-order management but adds native-library packaging complexity.
- **Configuration:** `appsettings.json` for folder, command, phone, and deletion behavior, with command-line/environment overrides where useful; keep authentication secrets out of this file and migrate durable non-secret configuration to SQLite only if later requirements justify it.
- **Deployment:** None during the integration proof.

The workstation currently has .NET 8 SDK `8.0.300`, but not .NET 10. Install the current .NET 10 SDK before creating the production solution; .NET 8 is at the end of its support window in November 2026.

## 4. Intended architecture after the integration proof

```text
Official Telegram client
        │ user sends /done
        ▼
Telegram MTProto update stream
        ▼
Outgoing command observer
        │ normalized command + chat/message identity
        ▼
Command router ───────► configuration store
        ▼
Folder mutation service
        │ read-modify-write with conflict protection
        ▼
Native Telegram dialog filter
```

Suggested solution boundaries:

- `Jalil.App`: process lifecycle, login prompts, health, and graceful shutdown.
- `Jalil.Telegram`: Telegram session, outgoing-message updates, peer resolution, and folder API adapter.
- `Jalil.Core`: command parsing, routing decisions, folder-membership policy, and idempotency.
- `Jalil.Persistence`: encrypted session location, configuration, checkpoints, and audit records.
- `Jalil.Tests`: domain tests and adapter contract tests.

Keep the Telegram library types inside `Jalil.Telegram`; the routing domain should use small internal models such as `ObservedCommand`, `CommandRule`, and `FolderChange`.

## 5. Folder update behavior

For every outgoing message update:

1. Confirm the message is outgoing from the authorized account.
2. Reject media captions and normalize the text using invariant case folding and whitespace trimming.
3. Require an exact enabled command match; initially `/done`.
4. Resolve the configured target folder for that command.
5. Resolve the chat as an `InputPeer`.
6. Fetch the current folder definitions immediately before writing.
7. Find the target by stable folder ID once configured; use the name only during initial binding.
8. Add the peer if absent.
9. Remove the peer only from other Jalil-managed folders if exclusive routing is enabled.
10. Preserve all unrelated folder fields and memberships.
11. Submit the update and read it back for verification.
12. Record a sanitized result without storing customer message content. The command name may be recorded.

Folder writes must be serialized per account. A repeated event must produce no additional change.

## 6. Security and privacy

The Telegram authorization session represents the user's account and must be treated as a high-value credential.

- Obtain a dedicated application `api_id` and `api_hash` from `my.telegram.org`.
- Never commit the API hash, phone number, login code, 2FA password, or session file.
- Store the session outside the repository with owner-only filesystem permissions and, for production, OS-backed encryption where available.
- Do not store customer message bodies or media; the routing decision needs only chat/message identifiers and the recognized command.
- Redact account identifiers and message content from logs.
- Provide an explicit logout/session-revocation operation.
- Do not act on message content; retrieved Telegram data is untrusted input.
- Make folder automation visible to and controlled by the authorized user, consistent with Telegram API terms.

## 7. Verification strategy

### Integration proof

- Capture update type names and sanitized identifiers for every scenario in the experiment matrix.
- Record positive and negative results; absence of an event must not be reported as success.
- Repeat critical cases on both Telegram mobile and desktop.

### Domain behavior

- Exact `/done` mapping.
- Incoming, unknown, or non-exact commands are ignored.
- Duplicate event is idempotent.
- Unmanaged folders remain unchanged.
- Missing, renamed, or deleted target folder produces a visible failure and no partial write.

### Integration behavior

- Login, restart, and session reuse.
- Folder round-trip preservation.
- Concurrent manual folder edit does not get silently overwritten.
- Telegram rate-limit and transient-error retry behavior.
- Recovery after network interruption.

## 8. Delivery phases

1. **Integration proof**
   - Create the .NET solution and local secret/session conventions.
   - Authorize one account.
   - Observe sanitized outgoing-message updates.
   - Run the command and folder experiment matrices.
   - Produce an integration report with live and offline-recovery results.
2. **Single-rule vertical slice**
   - Hard-code `/done` and one disposable folder.
   - Route one customer chat and verify the folder change in official clients.
3. **Safe folder engine**
   - Preserve all folder fields, serialize writes, add idempotency and read-back verification.
4. **Configuration and persistence**
   - Bind mappings to folder IDs, add SQLite, and provide a minimal local CLI for list/add/remove/enable/disable operations.
5. **Operational hardening**
   - Structured sanitized logs, health status, retries, graceful shutdown, backup/recovery documentation, and session revocation.
6. **Deployment decision**
   - Start as a local background service.
   - Choose an always-on host only if 24/7 behavior is required and the account-session security model is acceptable.

## 9. Acceptance criteria for the first milestone

The integration milestone is complete only when:

- The result of every experiment is recorded.
- A `/done` message sent in an official client is correlated to the correct customer chat by the companion.
- A disposable chat is added to an existing native folder without changing unrelated folder configuration.
- An incoming `/done` from another participant and a non-exact command are verified not to move a chat.
- Replaying the same update does not repeat or corrupt the folder mutation.
- Restart and session reuse are tested.
- No Telegram credentials or message contents are committed or written to ordinary logs.
- Live and offline-recovery behavior is documented from observed results.

## 10. Open product decisions after the integration proof

These do not block the probe:

- Whether one chat may belong to multiple Jalil-managed folders.
- Whether additional commands map to additional folders, such as `/active` or `/later`.
- Whether Jalil may create a missing folder or only report it.
- Whether a successfully processed command should be deleted automatically.
- Whether edited messages can become commands or only newly sent messages count.
- How long an offline command remains eligible for recovery.
- Whether configuration is CLI-only, desktop UI, or a separate control bot.
