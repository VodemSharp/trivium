---
title: Telegram coverage
last_reviewed: 2026-11-16
code_globs:
  - src/Trivium/**
  - Directory.Packages.props
---

# Telegram coverage

[← Back to AGENTS.md](../../AGENTS.md)

**Related:** [Docs index](../README.md) · [Roadmap](../plan/roadmap.md) ·
[Architecture](../fundamental/architecture.md) · [Diagnostics](diagnostics.md)

## Baseline

| Item             | Version                         |
| ---------------- | ------------------------------- |
| `Telegram.Bot`   | `22.10.3.3`                     |
| Telegram Bot API | 10.3 (2026-08-24)               |
| Trivium target   | .NET 10                         |
| Update types     | 28 values (including `Unknown`) |

Trivium is a routing/hosting layer, not a second Bot API client. **Every Bot API method remains available** through
`ITelegramBotClient`. This document tracks what Trivium adds on top: routing, typed contexts, helpers and samples.

## Support levels

| Level           | Meaning                                                                               |
| --------------- | ------------------------------------------------------------------------------------- |
| **Routed**      | The update type has a route attribute and reaches a handler.                          |
| **Typed**       | `TriviumContext` exposes the update payload with a precise .NET type.                 |
| **Helper**      | Trivium ships a convenience method for the common operation.                          |
| **Passthrough** | Use `ITelegramBotClient` directly; Trivium documents the pattern but adds no wrapper. |
| **Sample**      | A runnable sample demonstrates the flow end-to-end.                                   |
| **N/A**         | Not applicable to a server-side framework (e.g. client-side Mini App storage).        |

A feature can be Routed + Typed + Passthrough without a helper. That is intentional: the helper surface stays small.

## Update types

`UpdateKind` mirrors `Telegram.Bot.Types.Enums.UpdateType`. Attribute names are the convenience layer; every route can
also be declared with `[UpdateHandler(UpdateKind.X)]`.

|   # | `UpdateType`               | Telegram.Bot property / type                            | Convenience attribute        | Level                 | Notes                                         |
| --: | -------------------------- | ------------------------------------------------------- | ---------------------------- | --------------------- | --------------------------------------------- |
|   1 | `Message`                  | `Message` / `Message`                                   | `[MessageCommand]`           | Routed, Typed, Helper | Exact/parameterized/default, caption fallback |
|   2 | `EditedMessage`            | `EditedMessage` / `Message`                             | `[EditedMessageCommand]`     | Routed, Typed         | Same argument model                           |
|   3 | `ChannelPost`              | `ChannelPost` / `Message`                               | `[ChannelPostCommand]`       | Routed, Typed         | Actor is the channel, not a user              |
|   4 | `EditedChannelPost`        | `EditedChannelPost` / `Message`                         | `[EditedChannelPostCommand]` | Routed, Typed         |                                               |
|   5 | `CallbackQuery`            | `CallbackQuery` / `CallbackQuery`                       | `[CallbackQuery]`            | Routed, Typed, Helper | Typed `[CallbackData]` or raw path            |
|   6 | `InlineQuery`              | `InlineQuery` / `InlineQuery`                           | `[InlineQuery]`              | Routed, Typed, Helper | `AnswerInlineQuery` helper                    |
|   7 | `ChosenInlineResult`       | `ChosenInlineResult` / `ChosenInlineResult`             | `[ChosenInlineResult]`       | Routed, Typed         |                                               |
|   8 | `ShippingQuery`            | `ShippingQuery` / `ShippingQuery`                       | `[ShippingQuery]`            | Routed, Typed, Helper | Answer success/failure                        |
|   9 | `PreCheckoutQuery`         | `PreCheckoutQuery` / `PreCheckoutQuery`                 | `[PreCheckoutQuery]`         | Routed, Typed, Helper | Answer with/without error                     |
|  10 | `Poll`                     | `Poll` / `Poll`                                         | `[Poll]`                     | Routed, Typed         | Poll state updates                            |
|  11 | `PollAnswer`               | `PollAnswer` / `PollAnswer`                             | `[PollAnswer]`               | Routed, Typed         | Includes option persistent ids                |
|  12 | `ChatMember`               | `ChatMember` / `ChatMemberUpdated`                      | `[ChatMember]`               | Routed, Typed         | `ChatMemberUpdated`, not `ChatMember`         |
|  13 | `MyChatMember`             | `MyChatMember` / `ChatMemberUpdated`                    | `[MyChatMember]`             | Routed, Typed         | Bot's own membership                          |
|  14 | `ChatJoinRequest`          | `ChatJoinRequest` / `ChatJoinRequest`                   | `[ChatJoinRequest]`          | Routed, Typed, Helper | Approve/decline; 10.1 query answer            |
|  15 | `BusinessConnection`       | `BusinessConnection` / `BusinessConnection`             | `[BusinessConnection]`       | Routed, Typed         | Connection enabled/disabled                   |
|  16 | `BusinessMessage`          | `BusinessMessage` / `Message`                           | `[BusinessMessage]`          | Routed, Typed, Sample | Business account inbox                        |
|  17 | `EditedBusinessMessage`    | `EditedBusinessMessage` / `Message`                     | `[EditedBusinessMessage]`    | Routed, Typed, Sample |                                               |
|  18 | `DeletedBusinessMessages`  | `DeletedBusinessMessages` / `BusinessMessagesDeleted`   | `[DeletedBusinessMessages]`  | Routed, Typed, Sample |                                               |
|  19 | `MessageReaction`          | `MessageReaction` / `MessageReactionUpdated`            | `[MessageReaction]`          | Routed, Typed         | Added/removed reactions                       |
|  20 | `MessageReactionCount`     | `MessageReactionCount` / `MessageReactionCountUpdated`  | `[MessageReactionCount]`     | Routed, Typed         | Aggregate counts                              |
|  21 | `ChatBoost`                | `ChatBoost` / `ChatBoostUpdated`                        | `[ChatBoost]`                | Routed, Typed         |                                               |
|  22 | `RemovedChatBoost`         | `RemovedChatBoost` / `ChatBoostRemoved`                 | `[RemovedChatBoost]`         | Routed, Typed         |                                               |
|  23 | `PurchasedPaidMedia`       | `PurchasedPaidMedia` / `PaidMediaPurchased`             | `[PurchasedPaidMedia]`       | Routed, Typed         | Stars payment for paid media                  |
|  24 | `GuestMessage`             | `GuestMessage` / `Message`                              | `[GuestMessage]`             | Routed, Typed, Sample | Guest mode (Bot API 10.0)                     |
|  25 | `ManagedBot`               | `ManagedBot` / `ManagedBotUpdated`                      | `[ManagedBot]`               | Routed, Typed         | Managed bot created/token changed             |
|  26 | `Subscription`             | `Subscription` / `BotSubscriptionUpdated`               | `[Subscription]`             | Routed, Typed         | User payment subscription changed             |
|  27 | `StoppedMessageGeneration` | `StoppedMessageGeneration` / `MessageGenerationStopped` | `[StoppedMessageGeneration]` | Routed, Typed         | User stopped draft generation                 |
|  28 | `Unknown`                  | no payload                                              | `[UpdateHandler]` catch-all  | Routed                | Logged; never throws                          |

### Update-kind rules

- `UpdateKind` is derived from `Update.Type`, not by probing nullable properties.
- A payload property that is `null` while `Type` says otherwise is a malformed update; it is logged and routed to the
  catch-all, not dereferenced with `!`.
- `ChannelPost`/`EditedChannelPost` have a chat but no user actor; role/state guards that require a user must deny or
  skip explicitly.
- `MyChatMember` and `ChatMember` both carry `ChatMemberUpdated`; they are distinct update kinds but the same payload
  type.
- `BusinessMessage`/`EditedBusinessMessage`/`GuestMessage` are `Message` payloads delivered through different update
  slots. `TriviumContext` exposes the payload, and the update kind tells the handler which workflow it belongs to.

## Bot API 8–10 feature matrix

This matrix tracks **Trivium-provided support**, not the existence of the Bot API method. Every method is available
via `ITelegramBotClient` (Passthrough) even when Trivium adds no helper.

| Feature                         | Representative API / types                                                                                                                      | Support in 1.0                                  | Milestone |
| ------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------- | --------- |
| Business connections            | `BusinessConnection`, `GetBusinessConnection`                                                                                                   | Routed, Typed, Passthrough                      | M7        |
| Business messages               | `BusinessMessage`, `ReadBusinessMessage`, `DeleteBusinessMessages`                                                                              | Routed, Typed, Sample                           | M7        |
| Business profile                | `SetBusinessAccountName/Username/Bio/ProfilePhoto`, `RemoveBusinessAccountProfilePhoto`                                                         | Passthrough, Sample                             | M7        |
| Business gifts/stars            | `GetBusinessAccountGifts`, `GetBusinessAccountStarBalance`, `TransferBusinessAccountStars`, `ConvertGiftToStars`, `UpgradeGift`, `TransferGift` | Passthrough, Sample                             | M7        |
| Business stories                | `PostStory`, `RepostStory`, `EditStory`, `DeleteStory`                                                                                          | Passthrough, Sample                             | M7        |
| Business checklists             | `SendChecklist`, `EditMessageChecklist`                                                                                                         | Passthrough, Sample                             | M7        |
| Telegram Stars                  | `GetMyStarBalance`, `GetStarTransactions`, `RefundStarPayment`, `EditUserStarSubscription`                                                      | Passthrough, Helper (refund)                    | M7        |
| Gifts                           | `GetAvailableGifts`, `SendGift`, `GiftPremiumSubscription`, `GetUserGifts`, `GetChatGifts`                                                      | Passthrough, Sample                             | M7        |
| Unique gifts                    | `UniqueGift`, `UpgradeGift`, `ConvertGiftToStars`, `TransferGift`                                                                               | Passthrough, Sample                             | M7        |
| Gift presentation               | `GiftBackground`, `UniqueGiftColors`, gift variant fields (9.3)                                                                                 | Passthrough                                     | M7        |
| Stories                         | `Story`, story service messages                                                                                                                 | Routed where applicable, Passthrough            | M7        |
| Checklists                      | `Checklist`, `ChecklistTasksDone`, `ChecklistTasksAdded`, reply to task                                                                         | Typed where applicable, Passthrough             | M7        |
| Rich messages                   | `SendRichMessage`, `SendRichMessageDraft`, `InputRichMessage`, rich blocks                                                                      | Helper (`SendHtml`/`EditHtml`), Passthrough     | M7        |
| Rich message buttons            | `RichMessageButton`, `RichTextButton` (10.3)                                                                                                    | Passthrough                                     | M7        |
| Ephemeral messages              | `EphemeralMessageParameters`, send/edit/delete, `receiver_user`, `ephemeral_message_id`                                                         | Typed, Passthrough, Helper (reply)              | M7        |
| Guest mode                      | `GuestMessage`, `AnswerGuestQuery`, guest caller fields                                                                                         | Routed, Typed, Helper (answer), Sample          | M7        |
| Managed bots                    | `ManagedBot` update, `GetManagedBotToken`, `ReplaceManagedBotToken`, access settings                                                            | Routed, Typed, Passthrough                      | M7        |
| Communities                     | `Community`, `CommunityChatAdded/Removed/Joined` service messages                                                                               | Typed service messages, Passthrough             | M7        |
| Suggested posts                 | `ApproveSuggestedPost`, `DeclineSuggestedPost`, `SuggestedPost*` service messages                                                               | Typed service messages, Passthrough, Helper     | M7        |
| Direct messages in channels     | `DirectMessagesTopic`, `direct_messages_topic_id`, `DirectMessagePriceChanged`                                                                  | Typed, Passthrough                              | M7        |
| Live photos                     | `SendLivePhoto`, `InputMediaLivePhoto`, `PaidMediaLivePhoto`                                                                                    | Passthrough, Helper (send)                      | M7        |
| Poll media                      | `PollMedia`, `InputPollMedia`, `InputPollOptionMedia`                                                                                           | Typed, Passthrough                              | M7        |
| Polls 9.6/10.x                  | multiple correct answers, options added/deleted, descriptions, country codes, members-only, revoting/shuffle, persistent ids                    | Typed, Passthrough                              | M7        |
| Reactions                       | `SetMessageReaction`, `DeleteMessageReaction`, `DeleteAllMessageReactions`, `MessageReaction*` updates                                          | Routed, Typed, Helper                           | M7        |
| Boosts                          | `ChatBoost`, `RemovedChatBoost`, `GetUserChatBoosts`                                                                                            | Routed, Typed, Passthrough                      | M7        |
| Paid media                      | `PurchasedPaidMedia`, `SendPaidMedia`, paid media types                                                                                         | Routed, Typed, Passthrough                      | M7        |
| Verification                    | `VerifyUser`, `VerifyChat`, `RemoveUserVerification`, `RemoveChatVerification`                                                                  | Passthrough, Helper                             | M7        |
| Profile photos                  | `SetMyProfilePhoto`, `RemoveMyProfilePhoto`                                                                                                     | Passthrough                                     | M7        |
| Profile audios                  | `GetUserProfileAudios`                                                                                                                          | Passthrough                                     | M7        |
| First profile audio             | `ChatFullInfo.FirstProfileAudio` (9.4)                                                                                                          | Passthrough                                     | M7        |
| Video qualities                 | `Video.Qualities`, `VideoQuality` (9.4)                                                                                                         | Passthrough                                     | M7        |
| Private-chat topics             | `has_topics_enabled`, `message_thread_id`, `is_topic_message` in private chats (9.3/9.4)                                                        | Typed, Passthrough                              | M7        |
| Topic permissions               | `allows_users_to_create_topics` (9.4)                                                                                                           | Passthrough                                     | M7        |
| User rating                     | `ChatFullInfo.Rating`, `UserRating` (9.3)                                                                                                       | Passthrough                                     | M7        |
| Personal chat messages          | `GetUserPersonalChatMessages` (10.0)                                                                                                            | Passthrough                                     | M7        |
| Emoji status                    | `SetUserEmojiStatus`                                                                                                                            | Passthrough                                     | M7        |
| Member tags                     | `SetChatMemberTag`, `sender_tag`, `ChatMemberMember.Tag`                                                                                        | Passthrough, Typed                              | M7        |
| Owner changes                   | `ChatOwnerLeft`, `ChatOwnerChanged` service messages                                                                                            | Typed, Passthrough                              | M7        |
| Paid message price changes      | `PaidMessagePriceChanged` (9.0)                                                                                                                 | Typed, Passthrough                              | M7        |
| Direct message price changes    | `DirectMessagePriceChanged` (9.1)                                                                                                               | Typed, Passthrough                              | M7        |
| Message drafts                  | `SendMessageDraft`, `SendRichMessageDraft`, `StoppedMessageGeneration`                                                                          | Routed, Typed, Passthrough                      | M7        |
| Chat join request queries       | `AnswerChatJoinRequestQuery`, `SendChatJoinRequestWebApp` (10.1)                                                                                | Routed (`ChatJoinRequest`), Helper, Passthrough | M7        |
| Mini Apps                       | `AnswerWebAppQuery`, `SavePreparedInlineMessage`, `SavePreparedKeyboardButton`, `initData` validation                                           | Helper (validation), Passthrough                | M7/M8     |
| Webhook management              | `SetWebhook`, `DeleteWebhook`, `GetWebhookInfo`, secret, allowed updates, max connections                                                       | Helper (hosting), Typed options                 | M3/M4     |
| Command catalog                 | `SetMyCommands`, scopes, languages, ephemeral commands                                                                                          | Helper (sync + localization)                    | M3/M7     |
| Custom emoji in buttons         | `icon_custom_emoji_id` (9.4)                                                                                                                    | Passthrough                                     | M7        |
| Disabled buttons                | `disabled` / `DisabledButton` (10.3)                                                                                                            | Passthrough                                     | M7        |
| Force reply in inline keyboards | `force_reply` (10.3)                                                                                                                            | Passthrough                                     | M7        |
| Date/time entities              | `MessageEntity` type `date_time` (9.5)                                                                                                          | Passthrough                                     | M7        |
| Poll option add/delete          | `PollOptionAdded`, `PollOptionDeleted` service messages                                                                                         | Typed, Passthrough                              | M7        |
| Paid broadcasts                 | BotFather setting; rate limit up to 1000 msg/s, 0.1 Stars/message over 30/s                                                                     | Documentation only                              | M8        |
| Bot-to-bot communication        | BotFather setting; no API method                                                                                                                | Documentation only                              | M7        |
| Mini App device/secure storage  | Client-side JavaScript APIs                                                                                                                     | N/A                                             | —         |

## Helper surface

Helpers exist to remove repetitive boilerplate, not to fork the Bot API. The 1.0 set:

| Helper                            | Purpose                                                           |
| --------------------------------- | ----------------------------------------------------------------- |
| `ctx.ReplyAsync`                  | Reply to the current message with `ReplyParameters` correctly set |
| `ctx.SendAsync`                   | Send to the current chat without repeating `ChatId`               |
| `ctx.EditAsync`                   | Edit the current message (text/caption/media)                     |
| `ctx.DeleteAsync`                 | Delete the current message                                        |
| `ctx.AnswerCallbackAsync`         | Answer a callback query, with optional text/alert                 |
| `ctx.AnswerInlineAsync`           | Answer an inline query                                            |
| `ctx.AnswerShippingAsync`         | Answer a shipping query, success or error                         |
| `ctx.AnswerPreCheckoutAsync`      | Answer a pre-checkout query, success or error                     |
| `ctx.AnswerGuestAsync`            | Answer a guest query                                              |
| `ctx.DownloadFileAsync`           | Download a file with size limits and cancellation                 |
| `SendHtmlAsync` / `EditHtmlAsync` | Thin wrappers over Telegram.Bot's `SendHtml`/`EditHtml`           |
| `MessageLink()`                   | Expose Telegram.Bot's helper                                      |
| `IsServiceMessage`                | Expose Telegram.Bot's helper                                      |
| `ChatMember.IsInChat` / `IsAdmin` | Expose Telegram.Bot's helpers                                     |
| `validateWebAppInitData`          | Mini App `initData` validation (M7/M8)                            |

Rules:

- Helpers never swallow `ApiRequestException`; they surface it with bot/chat context.
- Helpers never add a second name for an existing Telegram.Bot method.
- Helpers are extension methods or `TriviumContext` members; they do not allocate a second client.
- Advanced options always remain reachable via the `ITelegramBotClient` overloads.

## Commands and localization

- The generator emits a command catalog from `[MessageCommand]` metadata:
  `Command`, `Description`, `Scope`, `Language`, `Hidden`.
- `SetMyCommands` sync is opt-in and supports:
  - default, all private chats, all group chats, all chat administrators;
  - specific chat, chat administrators, chat member;
  - per-language descriptions with fallback.
- Ephemeral commands: Telegram supports `is_ephemeral`; Trivium exposes it as metadata and documents the behavior.
- Command descriptions should be short, imperative and localized. A non-hidden route without a description produces a
  `TRIVIUM0010` warning.
- Sync never deletes manually managed commands unless `CommandSyncMode.Replace` is explicit.

## Coverage tests

The matrix is only credible if it is tested:

- **One test per update kind:** build a realistic `Update`, dispatch it, assert the handler ran with the right typed
  payload and actor.
- **Golden JSON fixtures:** serialize Telegram-shaped JSON, deserialize with source-gen, dispatch, assert. Fixtures are
  versioned with the Bot API baseline.
- **Drift check:** a test enumerates `Enum.GetValues<UpdateType>()` and asserts every value has a route attribute and a
  fixture. Adding a new Telegram.Bot update kind fails the build until covered.
- **Feature matrix check:** a test reads this document's update table (or a machine-readable companion) and fails on
  missing rows. The matrix is part of the definition of done.
- **Unknown update:** a synthetic unknown update is logged and routed to the catch-all without throwing.
- **Malformed payload:** `Type` says `Message` but `Message` is null → handled, not `NullReferenceException`.

## Maintenance cadence

Telegram ships Bot API updates every few months; Telegram.Bot follows. When a new version lands:

1. Bump `Telegram.Bot` in `Directory.Packages.props`.
2. Run the drift test; it lists new `UpdateType` values and missing fixtures.
3. Add update kinds, attributes, typed accessors and fixtures.
4. Update this matrix and the release notes.
5. Add helpers only when the pattern is clearly repetitive; otherwise mark Passthrough.
6. Re-run AOT/trim analysis and the full integration suite.

No release ships with an unclassified update kind.
