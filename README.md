# Encore for Unity

A C# API over the native Encore Android and iOS SDKs, and over the Encore web SDK for WebGL builds. Phase 1: post-action reward placements.

## Requirements

- Unity 2022.3 or newer, for Android and iOS builds. In the Editor and on other targets, every placement reports not presented.
- [External Dependency Manager for Unity](https://openupm.com/packages/com.google.external-dependency-manager/). It reads `Editor/EncoreDependencies.xml`, which pins the native SDKs. Without it, the native SDKs are not added to the build.
- Android: minimum API level 24 and target API level 35.
- iOS: deployment target 15.0.
- WebGL: tested on Unity 6.3 in desktop Chrome, iOS Safari and Android Chrome, and in a cross-origin iframe on iOS Safari. On a phone in landscape the offer sheet shows no offers, so present offers in portrait there. The package bundles `@encorekit/web-sdk` 2.4.0 (`Runtime/Plugins/WebGL/encore-web-sdk.jspre`), so no template change is needed. Unity 2022.3 does not support mobile browsers.

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

Served looks also read brand colours and your own prize, on Android, iOS and WebGL (where the web SDK does not draw or grant the prize yet):

```csharp
var result = await Encore.Placement("level_complete")
    .UseCase(UseCase.RewardUsers)
    .Appearance(new AppearanceConfig { AccentColor = "#FF8800", BackgroundColor = "#1B2A41", TextColor = "#FFFFFF" })
    .PublisherReward(new PublisherReward { Id = "coins_100", Title = "100 coins" })
    .ShowAsync();

// Grant the prize only when the player said they finished and Encore granted it.
if (result.Claim?.UserConfirmedCompletion == true) GrantCoins(100);
```

Your own button labels, a loading state, and a warm-up when the screen opens:

```csharp
var reward = Encore.Placement("level_complete").UseCase(UseCase.RewardUsers);
reward.Prefetch(); // iOS only for now
moreGiftsButton.SetActive(await reward.HasOffersAsync()); // Android and iOS; false on WebGL for now

reward.OnLoadingStateChange += loading => spinner.SetActive(loading);
var result = await reward.Cta("Claim my gift").Decline("Maybe later").Badge("Top pick").ShowAsync();

Encore.IsClaimEnabled = false;                              // grey out the claim button
Encore.PublisherRewardPolicy = new PublisherRewardPolicy(5); // at most 5 prizes a day
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
