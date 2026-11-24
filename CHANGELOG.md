# Changelog

All notable changes to `com.encorekit.unity`. `make release` turns the Unreleased section into the new version's section.

## [Unreleased]

## [2.8.1] - 2026-11-24

### Changed

- Native pins: `com.encorekit:encore` 2.8.0 and the `EncoreKit` 2.8.1 pod.

### Fixed

- iOS (from EncoreKit 2.8.1): a trigger look's motion now runs. Before, an element with an entrance stayed at its
  starting pose, so a look whose picture fades in from transparent drew no picture, and loops and marquees stood still.
- A `UseCase` outside the enum gives a hidden trigger or claim and a warning (from `LogLevel.Warn`) instead of an
  exception. `GiftsButton.Create`, `ClaimReminder.Create` and `EncoreTrigger.Create` threw; `Encore.Claim` stayed
  hidden without saying why. Android, iOS and WebGL.
- A throw in an iOS or WebGL native callback is contained and reported, and never unwinds into the native frame that
  called back. Android already contained it in the Kotlin bridge.
- A NaN or an infinity is left out of the bridge JSON instead of being written as `NaN`, which no bridge could read.

## [2.8.0] - 2026-11-22

### Added

- The trigger look fields on `AppearanceConfig`: `FontFamily`, `CountdownFontFamily`, `CornerRadius`, `Shadow` (an
  `EncoreShadow`: `Flat`, `Soft` or `Strong`), `BorderWidth`, `BorderColor`, `BackgroundImage`,
  `BackgroundImageCapInsets` (an `EncoreEdgeInsets`) and `IconImage`. They style `GiftsButton`, `ClaimReminder` and
  `EncoreTrigger` as the native `AppearanceConfig` does, on Android and iOS; needs the native 2.6.0 SDKs. Fonts and
  images are names the native app ships: fonts by the sheet font lookup; images from the iOS main bundle and Android
  drawable resources. A blank name or a non-finite length is not sent; the native SDK clamps a negative length
  and drops cap insets with a negative or non-finite side. A font name not found draws the system font; an image name keeps the look's
  own background or icon. A placement's appearance ignores
  them, as the native sheet does. WebGL ignores them, because web SDK 2.6.0 takes none of them.
- `SheetAppearance.Components`: reward sheet styles per component from app code, keyed by `SheetComponent` (the
  catalogue names as constants, such as `SheetComponent.PrimaryButton`, or any name). A `SheetComponentStyle` sets fill,
  border and content colours (one hex, or a light and dark pair), corner radius, border width, font role, images, a
  Lottie `Animation`, an `Enter` and a `Loop`. The Encore portal's Style editor wins per property, then app code, then
  the variant. Android, iOS and WebGL; the native SDK drops a value out of range with one warning.

- `ClaimedOffer.ClaimUrl`: the attributable link the user opened for a claim (iOS `claimURL`, Android and web
  `claimUrl`). It completes the offer as this user, so keep it out of logs and analytics. Null for demo offers and links
  with no transaction id. Android, iOS and WebGL.
- `TriggerOptions.SheetHeadline` and `SheetSubheadline`, and `EncoreTrigger.SetSheetText`: the headline and subheadline
  of the sheet a trigger opens (`GiftsButton`, `ClaimReminder`, `EncoreTrigger`, and the Inspector fields of their
  components), as the native `sheetHeadline` and `sheetSubheadline`. Null or blank keeps the sheet's own text. A change
  applies to the next sheet and redraws nothing. Android, iOS and WebGL.
- `GiftsButtonOptions.HostState`, `GiftsButton.SetHostState` and `EncoreGiftsButtonView.HostState`: your app's state for
  the gift, a `GiftsButtonHostState` (`Locked(progress)`, `Locked(completed, total)`, `Ready` or `Done`), as the native
  `GiftsButtonHostState`. While locked, a tap shows a hint and never opens the sheet. A change redraws in place with no
  new request. Android and iOS; WebGL ignores it, because web SDK 2.6.0 has none.
- `GiftsButton.ShowingChanged` and `EncoreGiftsButtonView.ShowingChanged`: true while a look is drawn and false while
  it draws nothing, on the first answer and on each change, as the native `onShowingChange`. Android and iOS; never
  raised on WebGL.
- `EncoreTrigger.SetVisible(float fraction)`: how much of a trigger the game shows, 0 to 1, for example under a panel
  of its own. Above 0 the trigger draws and the native SDK counts the impression on that share (`hostVisibleFraction`);
  0 hides it, and so does NaN or an infinity, as on `EncoreClaim`. `SetVisible(bool)` is unchanged and is the same as 1 or 0. Android and iOS; WebGL only shows or hides.
- `Encore.Claim(placement, ClaimOptions)`: an `EncoreClaim` for a gifts button or claim reminder the game draws itself,
  as the native `Encore.claim`. Read `State` (a `ClaimState`: `Hidden`, `Gifts` and `Thanks` with `OfferCount` and
  `LogoUrls`, or `Reminder` with the claim's details) or `HasOffers`, and draw it; `StateChanged` raises each change.
  `SetVisible(fraction)` counts the impression, `Tap` and `TapAsync` open the sheet and answer its result, `Dismiss`
  hides a reminder, `Result` raises every sheet's result. `ClaimOptions` and the setters take `AutoOpenOff`,
  `PublisherReward`, `SheetHeadline` and `SheetSubheadline`. `Dispose` releases the native claim; a tap in flight still
  answers. Android, iOS and WebGL (web SDK 2.6.0 `Encore.claim`, which takes the prize only at create). In the Editor
  it stays hidden.
- `Encore.IsEnabled` turns the whole SDK off (`false`) or back on (`true`) at runtime, for a game whose player turns
  off an in-app deals setting. Default `true`; set it any time, before or after `Configure`. `Configure` sends it
  before the native configure, so a game set off never starts the SDK. If the native SDK cannot take `false` there
  (a bridge older than the package), `Configure` leaves Encore unconfigured rather than start it, and can run again. While off the native SDK sends nothing (no
  analytics or app open, foreground and background events, no error reports, no background network), `Show` answers
  the new `NotPresentedReason.Disabled` with no UI, and triggers show nothing. A sheet already open finishes. Nothing
  dropped while off is sent later; turning it back on resumes the SDK's own work. Not persisted: every launch starts
  on. Android, iOS and WebGL; needs the native 2.7.0 SDKs, and `@encorekit/web-sdk` 2.7.0 for WebGL. A switch the
  SDK refuses is never cached: after `Configure`, `IsEnabled` keeps the value the SDK still has. An older bundled web
  SDK has no switch and is always on, so on WebGL `false` before `Configure` leaves Encore unconfigured, and after it
  `IsEnabled` stays `true`. The same fail-closed rule covers a native Android SDK older than 2.7.0: the bridge now
  answers whether the SDK took the switch. After a refused off, turning it on needs a new `Configure`.

### Changed

- Native pins: `com.encorekit:encore` 2.8.0 and the `EncoreKit` 2.8.0 pod.
- From the 2.8.0 native SDKs, on Android and iOS: Encore can draw a gift button or claim reminder look over the
  screen, as a popup, a half sheet or a full-screen cover, instead of in its spot. Encore picks the mode per look on the
  server; there is no setting. While a look is presented, the trigger's `Height` is 0 and `HeightChanged` reports 0:
  keep it placed and visible (do not `SetVisible(false)` it for having no height), since the native SDK uses its
  visible share to know when the spot is seen. Device limits cap how often a presented look shows. Served looks can
  also carry slot rules, a gift day count and entrance and loop motion, with no call.
- From the 2.7.0 native SDKs: the moment, the purchase amount and device fields (device model and type, OS version,
  connection type, low power mode) are now sent with offers requests. Update your App Store privacy and Play Data
  safety answers when you upgrade; see the native 2.7.0 changelogs.
- WebGL still bundles `@encorekit/web-sdk` 2.6.0, so on WebGL `IsEnabled` has no switch (see Added) and the 2.7.0 and
  2.8.0 web changes are not in this release.
- WebGL now passes the sheet fonts (`SheetAppearance.HeadingFontFamily` and `BodyFontFamily`) to the web SDK, which
  takes them as CSS font families. The page must load those fonts. 2.6.0 said WebGL ignores them.

## [2.6.0] - 2026-11-16

### Added

- `AppearanceConfig.Sheet`, a `SheetAppearance` with `HeadingFontFamily` and `BodyFontFamily`: the reward sheet's fonts,
  by the name the font has natively (an iOS font listed under `UIAppFonts`, an Android `res/font` resource). Android and
  iOS; needs the native 2.6.0 SDKs. WebGL ignores it. A blank or unknown name draws the system font.

### Changed

- Native pins: `com.encorekit:encore` 2.6.0, the `EncoreKit` 2.6.0 pod (from the `EncoreKit/cocoapods-specs` spec repo,
  as in 2.5.0), and `@encorekit/web-sdk` 2.6.0 for WebGL.
- From the 2.6.0 native and web SDKs: the reward sheet draws the skin set for your game on Encore's side (colours,
  corners, borders and images per component) while the skin is on for the game, and its motion (entrances, finite
  loops, moves between screens and Lottie files) comes from the served look, with no call. On Android and iOS, with
  no skin the sheet draws exactly as before, and Lottie plays only when the game links it (lottie-ios 4.x,
  `com.airbnb.android:lottie` 5.2 or later); without it, the element draws as before.
- WebGL: web SDK 2.6.0 also changes three sheet looks with no skin. A sheet's colours no longer carry over to the next
  sheet, text on a light accent colour is black or white by contrast instead of always white, and a gift button's
  accent colour now reaches the sheet it opens.
- From the 2.6.0 native SDKs: a "Yes, I did it" alone no longer counts as a completion, so a gift button or claim
  reminder that hides or thanks after a completion waits until the claim is verified or granted. Android and iOS.
- Server behaviour, on any version: a user whose age you send is under 13 gets no offers.
- WebGL: web SDK 2.6.0 does not configure with an API key that is not a `pk_live_` or `pk_test_` key (or a legacy
  `encore_` or `enc_pub_` key), so a placeholder key shows no sheet. The package does not read that answer yet, so
  `Encore.IsConfigured` is still true.

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
