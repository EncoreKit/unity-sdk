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
appearance ignores `Sheet`. On WebGL the two names pass to the web SDK as CSS font families, so the page must load
those fonts (for example with an `@font-face` rule in the WebGL template). See
[docs.encorekit.com](https://docs.encorekit.com/publishers/unity/sdk-reference/appearance-config).

### Trigger look

A trigger's appearance (`GiftsButton`, `ClaimReminder`, `EncoreTrigger`) also takes the native look fields, on
Android and iOS. Each changes one value of the served look; its layout and size stay:

```csharp
gifts.SetAppearance(new AppearanceConfig
{
    FontFamily = "LuckiestGuy-Regular",          // every text run; CountdownFontFamily for the countdown's digits
    CornerRadius = 0,                            // points (dp on Android)
    Shadow = EncoreShadow.Strong,                // Flat removes the look's own
    BorderWidth = 2, BorderColor = "#FFD700",
    BackgroundImage = "card_gold",               // fills the outer shape above BackgroundColor; cropped without cap insets
    BackgroundImageCapInsets = new EncoreEdgeInsets(12, 16, 12, 16), // top, left, bottom, right: a nine-slice
    IconImage = "trophy",                        // in place of the gift or app icon
});
```

Fonts take the sheet font lookup above. Images are names the native app ships, not Unity assets: on iOS an image in
the main bundle (an asset catalog name, as `UIImage(named:)` loads it), on Android a drawable resource, found as fonts
are (`card_gold`, or the name in that form). Keep looked-up drawables from resource shrinking as for fonts, for example
`tools:keep="@font/*,@drawable/card_gold"`. Cap insets are in points of the image's own size, and `Left` is the
image's left edge in every layout direction. A blank name and a non-finite length are not sent; the native SDK clamps
a negative length and drops insets with a negative or non-finite side. A font name the platform cannot find draws the system font
(for `CountdownFontFamily`, the `FontFamily`); an image name keeps the look's own background or icon. On Android a
`drawable-night` image follows a live light or dark switch, and a forced `ColorScheme` of `Light` or `Dark` picks the
matching image; with `System` or unset, it follows the device. On iOS an asset catalog image follows the trait. A placement's
appearance ignores these fields, as the native sheet does. WebGL ignores them: web SDK 2.6.0 takes none of them.

### Sheet component styles

`SheetAppearance.Components` styles the parts of the reward sheet a variant tags, on Android, iOS and WebGL. Each
property is resolved on its own: the Encore portal's Style editor wins, then your app code, then the variant.

```csharp
.Appearance(new AppearanceConfig
{
    Sheet = new SheetAppearance
    {
        Components = { [SheetComponent.PrimaryButton] = new SheetComponentStyle { Fill = "#F5C52B", CornerRadius = 12 } },
    },
})
```

A value out of range is dropped with one warning. The advertiser's content (`OfferTitle`, `OfferDetail`, and the
`OfferLogo` image and animation) is not editable.

Your own button labels, a loading state, and a warm-up when the screen opens:

```csharp
var reward = Encore.Placement("level_complete").UseCase(UseCase.RewardUsers);
reward.Prefetch(); // iOS only for now
moreGiftsButton.SetActive(await reward.HasOffersAsync()); // Android, iOS and WebGL

reward.OnLoadingStateChange += loading => spinner.SetActive(loading);
var result = await reward.Cta("Claim my gift").Decline("Maybe later").Badge("Top pick").ShowAsync();

Encore.IsClaimEnabled = false; // grey out the claim button
```

To turn Encore off for a player who turns off an in-app deals setting, set `Encore.IsEnabled = false`. Set it any
time, before or after `Configure`. While it is off the native SDK sends nothing (no analytics or app open, foreground
and background events, no error reports, no background network), `Show` answers `NotPresentedReason.Disabled` with no
UI, and triggers show nothing. A sheet already open finishes. Nothing dropped while off is sent later, and turning it
back on resumes the SDK's own work. It is not saved: every launch starts on, so set it from the game's own setting at
launch. On WebGL it needs a bundled `@encorekit/web-sdk` 2.7.0 or later; an older bundle has no switch, so `false`
before `Configure` leaves Encore unconfigured rather than start an SDK it cannot turn off. Turning it back on does not
configure it: call `Configure` again after setting `true`. The same holds on Android with a native SDK older than
2.7.0 (the iOS bridge does not build against one).

```csharp
Encore.IsEnabled = PlayerPrefs.GetInt("deals", 1) == 1; // before Configure, so the SDK never starts while off
Encore.Configure("pk_live_...");
```

Embedded views draw above the game at a UI element's rect. Add a component to an empty `RectTransform`:
`EncoreGiftsButtonView` (the gift button), `EncoreClaimReminderView` (a banner for an offer the player claimed and has
not finished, with no height until there is one), or `EncoreTriggerView` (any trigger id). From code:

```csharp
var reminder = ClaimReminder.Create("home");
reminder.HeightChanged += height => LayoutAround(height);
reminder.SetFrame(screenRect);   // Unity screen pixels, origin bottom left; call again when it moves
reminder.SetSheetText("Your gift is still here"); // the sheet a tap opens; TriggerOptions.SheetHeadline at create
reminder.Dispose();              // when the screen goes away

var gifts = GiftsButton.Create("home", new GiftsButtonOptions { HostState = GiftsButtonHostState.Locked(2, 4) });
gifts.ShowingChanged += showing => giftsLabel.SetActive(showing);
gifts.SetHostState(GiftsButtonHostState.Ready);   // redraws in place; while locked a tap only hints
```

### Your own gift button

`Encore.Claim` drives a button the game draws itself. Encore decides what it shows; you draw each state:

```csharp
var claim = Encore.Claim("streak_done", new ClaimOptions { SheetHeadline = "Pick a gift" });
claim.StateChanged += state =>
{
    giftButton.SetActive(state.Kind != ClaimStateKind.Hidden);
    giftLabel.text = state.Kind == ClaimStateKind.Reminder ? $"Finish {state.AdvertiserName}" : $"{state.OfferCount} gifts";
};
claim.SetVisible(1);                                          // while the button is on screen; 0 when it leaves
giftButton.onClick.AddListener(async () => Debug.Log(await claim.TapAsync()));
claim.Dispose();                                              // with the button
```

Dispose the claim with its view: the native claim keeps the last visible share you sent until then.

Android, iOS and WebGL. In the Editor it stays hidden.

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
