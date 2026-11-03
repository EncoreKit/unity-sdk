# Changelog

All notable changes to `com.encorekit.unity`. `make release` turns the Unreleased section into the new version's section.

## [Unreleased]

## [2.5.0] - 2026-11-03

### Added

- Post-action reward placements: `Encore.Placement(...).UseCase(UseCase.RewardUsers).ShowAsync()` on Android, iOS and WebGL.
- The gift button (`EncoreGiftsButtonView`), which shows the served gift button trigger variant on Android, iOS and WebGL.
- Native pins: `com.encorekit:encore` 2.5.0, the `EncoreKit` 2.5.0 pod, and `@encorekit/web-sdk` 2.5.0 for WebGL.
  The pod resolves from the `EncoreKit/cocoapods-specs` spec repo, because CocoaPods trunk takes no new versions from
  2 December 2026; the game's other pods still resolve from trunk. A Podfile that External Dependency Manager writes
  needs no change. A game that writes its own Podfile adds `:source => 'https://github.com/EncoreKit/cocoapods-specs.git'` to
  its `EncoreKit` line.
- The Game Harness sample.
- The claim reminder (`ClaimReminder`, `EncoreClaimReminderView`), a banner for an offer the user claimed and has not finished, and any native trigger by id (`EncoreTrigger`, `EncoreTriggerView`). They share the gift button's host, so each has the same frame, visibility, height, live updates and `Dispose`. Android, iOS and WebGL. `GiftsButton` and `EncoreGiftsButtonView` are unchanged for a game.
- Setters that served looks read: `Placement.Appearance` and `Placement.PublisherReward`, and on the gift button `Icon`, `Alignment`, `Appearance` and `PublisherReward`, with `SetAlignment`, `SetAppearance` and `SetPublisherReward` for a live button. Android, iOS and WebGL; on WebGL the web SDK 2.5.0 takes the prize but does not draw or grant it, so `UserConfirmedCompletion` stays false there. On WebGL, `SetValues`, `SetAppearance` and `SetPublisherReward` mount the gift button again.
- Copy setters `Placement.Cta`, `Decline` and `Badge`, with the native rules: an empty string keeps the served copy, and only the reward use case reads them. Android and iOS; WebGL ignores them, because the web SDK 2.5.0 placement builder has no `cta`, `decline` or `badge`.
- `Encore.IsClaimEnabled` (Android and iOS apply a change to an open sheet; WebGL from the next `Show`) and `Encore.PublisherRewardPolicy`, the reward daily limit, clamped to 1 to 10 (Android and iOS; WebGL keeps the server default). Both can be set before `Configure`, which sends them, and both survive `Reset`.
- `Placement.Prefetch()`, which warms the offers so `Show` opens without a network round trip. iOS only: Android SDK 2.5.0 and web SDK 2.5.0 have no placement prefetch, so there it does nothing.
- `Placement.OnLoadingStateChange`, a C# event: `true` when `Show` starts and `false` when its result arrives, before the result callback, on the main thread, on every platform (Android SDK 2.5.0 has no loading callback, so C# raises it everywhere, as the Swift and web builders do).
- The purchase controller: `IEncorePurchaseController`, passed to `Encore.Configure`, buys the product an offer sells
  and answers `PurchaseResult.Purchased`, `Cancelled` or `Pending`. A throw is a failed purchase. Android and iOS.
- Android: the offer sheet draws in the game's own window, so Unity keeps running under it and a purchase the sheet
  asks for runs while it is open. The package's manifest sets `com.encorekit.offerSheetInHostWindow`; a game turns it
  off with `android:value="false"` and `tools:replace="android:value"`, and then Unity pauses under the sheet again.
  A game that stops its timers or audio in `OnApplicationPause` must do so itself while a sheet is open, for example
  from `Placement.OnLoadingStateChange` and the result callback.
- `Placement.HasOffers(Action<bool>)` and `HasOffersAsync()`: whether `Show` would find offers for this user now, for a
  button or entry point you show only when it opens something. Select `UseCase.RewardUsers`: `ReduceChurn` answers
  false, as the native SDKs do. Android, iOS and WebGL.
- The package names itself to the native SDK (`unity` and its version) before `Configure`, so Encore can tell Unity
  traffic apart in analytics and error reports. Each failure the package contains also goes to the SDK's error
  reports, with the call name and the exception type only, never a message. Android, iOS and WebGL.
- `UseCase.ReduceChurn`, the paywall decline use case, on Android, iOS and WebGL. A placement with no `UseCase` call
  is `ReduceChurn`, as in Swift and Android, so a reward placement must call `UseCase(UseCase.RewardUsers)`. The gift
  button stays reward only.
- `Encore.Configure(apiKey, purchaseController, options)` and `Configure(apiKey, options)`. A second `Configure` is now
  ignored, as the native SDKs ignore it.
- `EncoreOptions` with `Unlock` (`UnlockMode.Optimistic` or `Strict`) and `TransactionLinking`. WebGL reads `LogLevel` and `Unlock`.
- `PlacementOutcome.Kind`: `StrictUnlockVerified` outcomes now reach `Encore.Outcomes`, with `TransactionId`. Android,
  iOS and WebGL.
- Entitlements: `Encore.IsActive`, `IsActiveAsync`, `IsActivePublisher` and `RevokeEntitlements`. iOS only for
  `IsActive` and `IsActivePublisher` (they answer `false` on Android and WebGL; iOS 2.5.0 matches the type only, and its
  `Verified` scope also counts provisional grants); `RevokeEntitlements` on iOS and WebGL.
- Android only: `Encore.LinkPurchase`, `CurrentEntitlement` and `ClearOfferCache`. No-ops elsewhere.

### Fixed

- WebGL: a sheet that opens while the game is fullscreen no longer stays hidden behind it. The web SDK draws its sheet on the page, so the package leaves fullscreen as the sheet opens, and again if the game enters fullscreen while it is open. A fullscreen root that holds the page keeps fullscreen. The gift button and other triggers stay visible in fullscreen.
- iOS: a bad message from C# or a missing window can no longer crash the game. A payload with NaN, a NaN frame, a
  null callback and unreadable JSON are dropped, and a trigger with no root view reports height 0. A throw in a C#
  callback is contained, not passed into native code. Every callback reaches C# on the main thread, and a purchase the
  SDK ends at 300 s no longer stays open in the bridge.
- Android: every bridge entry point contains a throw, bad JSON and a null argument, so none reaches C# or the main
  looper, and calls stay in order on the main thread. A purchase request that cannot reach C# fails at once, not at
  the SDK's 300 s ceiling.
- WebGL: a throw from the web SDK or the Unity callback no longer breaks the game loop. A placement or revoke request
  gets exactly one reply, and a destroyed or replaced gift button sends nothing more.
- `Configure`, `Identify` and every other call no longer throw when the native layer throws. A `Configure` that threw
  leaves `Encore.IsConfigured` false, so it can run again. `Show` and the entitlement calls answer exactly once, one
  game handler that throws no longer stops the others, and a gift button frame with a NaN or an infinity is not sent.
- At the default `LogLevel.None` the SDK and its bridges log nothing of their own. So the "Already
  configured" and "created before Encore.Configure" messages, which every game saw, now show only from `LogLevel.Warn`. Contained failures log from
  `LogLevel.Warn` up (iOS subsystem `com.encorekit.unity`, Android tag `EncoreUnity`, the browser console on WebGL).
