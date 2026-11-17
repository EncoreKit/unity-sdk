# Encore for Unity

A C# API over the native Encore Android and iOS SDKs, and over the Encore web SDK for WebGL builds. Phase 1: post-action reward placements.

## Requirements

- Unity 2022.3 or newer, for Android and iOS builds. In the Editor and on other targets, every placement reports not presented.
- [External Dependency Manager for Unity](https://openupm.com/packages/com.google.external-dependency-manager/). It reads `Editor/EncoreDependencies.xml`, which pins the native SDKs. Without it, the native SDKs are not added to the build.
- Android: minimum API level 24 and target API level 35.
- iOS: deployment target 15.0.
- WebGL: tested on Unity 6.3 in desktop Chrome, iOS Safari and Android Chrome, and in a cross-origin iframe on iOS Safari. On a phone in landscape the offer sheet opens as a column in the middle of the screen. The package bundles `@encorekit/web-sdk` 2.6.0 (`Runtime/Plugins/WebGL/encore-web-sdk.jspre`), so no template change is needed. Unity 2022.3 does not support mobile browsers.

## Install

1. Add [External Dependency Manager for Unity](https://openupm.com/packages/com.google.external-dependency-manager/).
2. In the Package Manager, choose **Add package from git URL** and enter `https://github.com/EncoreKit/unity-sdk.git#vX.Y.Z`, with a version from its [releases](https://github.com/EncoreKit/unity-sdk/releases).

## Use

```csharp
using EncoreKit;

Encore.Configure("pk_live_...");
Encore.Identify(playerId);

var result = await Encore.Placement("level_complete")
    .UseCase(UseCase.RewardUsers)
    .Headline("Level 3 cleared!")
    .ShowAsync();

if (result.Claim?.TransactionId is string transactionId)
{
    // The player claimed an offer. Keep the transaction id; it carries the attribution.
}
```

Served looks also read your brand colours, on Android, iOS and WebGL:

```csharp
var result = await Encore.Placement("level_complete")
    .UseCase(UseCase.RewardUsers)
    .Appearance(new AppearanceConfig { AccentColor = "#FF8800", BackgroundColor = "#1B2A41", TextColor = "#FFFFFF" })
    .ShowAsync();
```

### Sheet fonts

The reward sheet takes two fonts, on Android and iOS: `HeadingFontFamily` for the text your sheet style in the Encore
portal marks as heading (such as the title and the primary button), and `BodyFontFamily` for all other text. A Unity
font asset is not a native font, so register the font with each platform and pass the name it has there:

```csharp
.Appearance(new AppearanceConfig
{
    Sheet = new SheetAppearance { HeadingFontFamily = "LuckiestGuy-Regular", BodyFontFamily = "Nunito-Regular" },
})
```

- iOS: add the font file to the Xcode project and list it under `UIAppFonts` in `Info.plist`. Pass its PostScript
  name, for example `Nunito-Regular`.
- Android: add the font file as a font resource (`res/font/nunito_regular.ttf`, in an Android library plugin). Pass
  the resource name, `nunito_regular`. A name that is not a resource is tried lowercased, with any character other
  than `a-z`, `0-9` and `_` as `_`, so `Nunito-Regular` also finds `nunito_regular`. The bridge finds the resource by name at runtime, so with
  resource shrinking on, keep the fonts: a `res/raw/keep.xml` with `tools:keep="@font/*"`.

A blank or unknown name, or a font that cannot load, draws the system font. Only a placement's appearance styles the sheet; a gift button's
appearance ignores `Sheet`. WebGL ignores both. See
[docs.encorekit.com](https://docs.encorekit.com/publishers/unity/sdk-reference/appearance-config).

Your own button labels, a loading state, and a warm-up when the screen opens:

```csharp
var reward = Encore.Placement("level_complete").UseCase(UseCase.RewardUsers);
reward.Prefetch(); // iOS only for now
moreGiftsButton.SetActive(await reward.HasOffersAsync()); // Android, iOS and WebGL

reward.OnLoadingStateChange += loading => spinner.SetActive(loading);
var result = await reward.Cta("Claim my gift").Decline("Maybe later").Badge("Top pick").ShowAsync();

Encore.IsClaimEnabled = false; // grey out the claim button
```

Embedded views draw above the game at a UI element's rect. Add a component to an empty `RectTransform`:
`EncoreGiftsButtonView` (the gift button), `EncoreClaimReminderView` (a banner for an offer the player claimed and has
not finished, with no height until there is one), or `EncoreTriggerView` (any trigger id). From code:

```csharp
var reminder = ClaimReminder.Create("home");
reminder.HeightChanged += height => LayoutAround(height);
reminder.SetFrame(screenRect);   // Unity screen pixels, origin bottom left; call again when it moves
reminder.Dispose();              // when the screen goes away
```

### Paywall offers and your purchase code

Offers after a paywall decline (`UseCase.ReduceChurn`) can sell a product. Encore decides when; your code buys it,
through your own store or billing library. Pass a purchase controller to `Configure`:

```csharp
public sealed class GamePurchases : IEncorePurchaseController
{
    public async Task<PurchaseResult> Purchase(PurchaseRequest request)
    {
        // request.ProductId, plus PromoOfferId (an App Store or Play offer) and, on Android, BasePlanId, when set.
        var outcome = await MyStore.Buy(request.ProductId);
        return outcome.Cancelled ? PurchaseResult.Cancelled : PurchaseResult.Purchased;
        // Throw for a real failure. Encore records it as failed, never as cancelled.
    }
}

Encore.Configure("pk_live_...", new GamePurchases(), new EncoreOptions { Unlock = UnlockMode.Optimistic });
var result = await Encore.Placement("paywall_decline").UseCase(UseCase.ReduceChurn).ShowAsync();
```

With no controller, Encore never asks for a purchase. On Android the sheet draws in the game's own window, so the game
keeps running under it and a purchase the sheet asks for runs while it is open. On WebGL the web SDK sells nothing, so
the controller is not called. `Encore.IsActive` reads the grants on iOS only; it answers `false` on Android and WebGL.
