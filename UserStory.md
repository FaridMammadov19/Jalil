# Jalil user story

## Goal

As the owner of a Telegram work account, I want to mark a finished customer conversation by sending `/done` in that conversation, so that the complete conversation appears in an existing native Telegram folder. For testing, I can send `/undo` to remove it again.

## Approved product decision

Jalil will be a single-user Telegram client companion built with Telegram's user API (MTProto). It is not a BotFather/Bot API bot, because Bot API bots cannot manage the user's native chat folders.

The first version runs locally on macOS or Windows. The computer running Jalil must be awake, online, and the process must be running. Server deployment and automatic startup can be added later.

## First-version behavior

1. The user creates the target native folder in Telegram.
2. Jalil signs in to the user's Telegram account with the user's own `api_id` and `api_hash`.
3. The user sends the visible message `/done` in a customer chat from an official Telegram client.
4. Jalil receives the outgoing-message update and identifies the containing conversation.
5. Jalil adds that conversation to the configured native folder while preserving unrelated folder configuration and membership.
6. If the user sends `/undo`, Jalil removes the conversation from the configured folder.
7. After a successful folder operation, Jalil deletes the command message for all participants.

## Acceptance criteria

- Exact outgoing `/done` adds the conversation and exact outgoing `/undo` removes it; matching is case-insensitive and ignores surrounding whitespace.
- Incoming commands, suffixed or embedded commands, edited messages, and media captions do not trigger an action.
- Sending the command again is safe and does not create duplicate membership.
- The folder change is visible in official Telegram clients.
- Existing folder title, icon, filters, pinned chats, includes, and excludes are preserved except for the required target-chat change.
- Customer message bodies and media are not persisted by Jalil.
- The same source works on macOS and Windows.
- Folder name, add command, remove command, phone number, and command-message deletion are configurable in `appsettings.json`.

## Initial limitations

- The first version supports one `/done` route and one existing regular Telegram folder.
- Shared/imported folders and secret chats are not supported.
- Read-only conversations cannot be marked because the user cannot send `/done` there.
- `/done` remains visible in the customer conversation.
- The first version adds the conversation to the target folder and preserves membership in all other folders.

## Superseded ideas

Earlier ideas used emoji reactions or forwarding a message to a Bot API bot. They were replaced by the outgoing `/done` MTProto design because native Telegram folder management is a user-account API capability.
