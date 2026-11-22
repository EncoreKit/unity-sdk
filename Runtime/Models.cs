using System;
using System.Collections.Generic;

namespace EncoreKit
{
    public enum LogLevel { None, Error, Warn, Info, Debug }

    public enum UseCase
    {
        // A brand-funded reward after the player did something. Claim only.
        RewardUsers,
        // Second-chance offers after a paywall decline. Offers that sell a product need an IEncorePurchaseController.
        ReduceChurn,
    }

    public sealed class UserAttributes
    {
        public string Email;
        public string FirstName;
        public string LastName;
        public string PhoneNumber;
        public string PostalCode;
        public string City;
        public string State;
        public string CountryCode;
        public string Latitude;
        public string Longitude;
        public int? Age;
        // The native SDK works the age out on the device; the date is never sent.
        public string DateOfBirth;
        public string Gender;
        public string SubscriptionTier;
        public string MonthsSubscribed;
        public string BillingCycle;
        public string LastPaymentAmount;
        public string LastActiveDate;
        public string TotalSessions;
        public Dictionary<string, string> Custom = new Dictionary<string, string>();
    }

    public enum ColorScheme { System, Light, Dark }

    // Brand colours for one presentation: #RGB, #RRGGBB or #RRGGBBAA. Null keeps the look's own colour.
    // The sheet reads the first three; the gift button reads them all.
    public sealed class AppearanceConfig
    {
        public string AccentColor;
        public string BackgroundColor;
        public string TextColor;
        public string SecondaryTextColor;
        public string TrackColor;
        public string OnAccentColor;
        // Null follows the app's setting in the Encore portal.
        public ColorScheme? ColorScheme;
        public string BackgroundEndColor;

        // The look fields below style a trigger only (each changes one value of the served look); the sheet ignores them.
        // Android and iOS; WebGL ignores them.
        // Fonts as for SheetAppearance. Null or blank draws the system font.
        public string FontFamily;
        // The countdown's digits. Null or not found draws FontFamily.
        public string CountdownFontFamily;
        // The outer corner radius, in points (dp on Android). Null or not finite keeps the look's own; the SDK clamps the range.
        public float? CornerRadius;
        // Null keeps the look's own shadow.
        public EncoreShadow? Shadow;
        // A stroke inside the outer shape, in points (dp on Android). Alone, it draws in the look's border colour, else the accent.
        public float? BorderWidth;
        // The stroke's hex colour. Alone, it draws at the look's border width, else 1.
        public string BorderColor;
        // An image the native app ships, by name: an iOS main bundle image, an Android drawable resource. Not a Unity asset.
        public string BackgroundImage;
        // Set, BackgroundImage stretches to the frame with these corners kept (a nine-slice); null crops it to fill.
        public EncoreEdgeInsets? BackgroundImageCapInsets;
        // An image name, as BackgroundImage, in the icon slot in place of the gift or app icon. A name not found keeps the look's own.
        public string IconImage;

        // The reward sheet's fonts and component styles. Triggers ignore it. Null keeps the sheet's own.
        public SheetAppearance Sheet;
    }

    // A drop shadow preset for a trigger's outer shape. Flat removes the look's own.
    public enum EncoreShadow { Flat, Soft, Strong }

    // Insets in points of the image's own size. Left is the image's left edge in every layout direction.
    // A negative or non-finite side drops the insets (the native SDK checks a negative side).
    public struct EncoreEdgeInsets
    {
        public float Top;
        public float Left;
        public float Bottom;
        public float Right;

        public EncoreEdgeInsets(float top, float left, float bottom, float right)
        {
            Top = top;
            Left = left;
            Bottom = bottom;
            Right = right;
        }
    }

    // The reward sheet's fonts and component styles. Fonts go by the name the app registered natively (an iOS font listed
    // under UIAppFonts, an Android res/font resource); a Unity font asset is not one. On WebGL they are CSS font families
    // the page must load. Blank or unknown draws the system font.
    public sealed class SheetAppearance
    {
        // The text the sheet's style in the Encore portal marks as heading, such as the title.
        public string HeadingFontFamily;
        // Every other text on the sheet.
        public string BodyFontFamily;
        // Styles per component the variant tags. The Encore portal's Style editor wins per property, then these, then
        // the variant. Android, iOS and WebGL. The advertiser's content (OfferTitle, OfferDetail, and the OfferLogo image
        // and animation) is not editable.
        public Dictionary<SheetComponent, SheetComponentStyle> Components = new Dictionary<SheetComponent, SheetComponentStyle>();
    }

    // A component name a variant tags its elements with. Any name works, so a component added after this release is
    // styleable too: new SheetComponent("newName"). The constants are the names known at this release.
    public sealed class SheetComponent : IEquatable<SheetComponent>
    {
        public string Name { get; }

        public SheetComponent(string name) { Name = name; }

        public static readonly SheetComponent SheetBackground = new SheetComponent("sheetBackground");
        public static readonly SheetComponent CloseButton = new SheetComponent("closeButton");
        public static readonly SheetComponent Title = new SheetComponent("title");
        public static readonly SheetComponent Subtitle = new SheetComponent("subtitle");
        public static readonly SheetComponent Footnote = new SheetComponent("footnote");
        public static readonly SheetComponent OfferRow = new SheetComponent("offerRow");
        public static readonly SheetComponent OfferRowSelected = new SheetComponent("offerRowSelected");
        public static readonly SheetComponent OfferCard = new SheetComponent("offerCard");
        public static readonly SheetComponent SelectionIndicator = new SheetComponent("selectionIndicator");
        public static readonly SheetComponent OfferLogo = new SheetComponent("offerLogo");
        public static readonly SheetComponent OfferTitle = new SheetComponent("offerTitle");
        public static readonly SheetComponent OfferDetail = new SheetComponent("offerDetail");
        public static readonly SheetComponent Badge = new SheetComponent("badge");
        public static readonly SheetComponent PrimaryButton = new SheetComponent("primaryButton");
        public static readonly SheetComponent SecondaryButton = new SheetComponent("secondaryButton");
        public static readonly SheetComponent LinkButton = new SheetComponent("linkButton");
        public static readonly SheetComponent FilterChip = new SheetComponent("filterChip");
        public static readonly SheetComponent PageIndicator = new SheetComponent("pageIndicator");
        public static readonly SheetComponent Footer = new SheetComponent("footer");
        public static readonly SheetComponent Panel = new SheetComponent("panel");
        public static readonly SheetComponent StepList = new SheetComponent("stepList");
        public static readonly SheetComponent StepMarker = new SheetComponent("stepMarker");
        public static readonly SheetComponent StepMarkerCurrent = new SheetComponent("stepMarkerCurrent");
        public static readonly SheetComponent StepTitle = new SheetComponent("stepTitle");
        public static readonly SheetComponent StepDetail = new SheetComponent("stepDetail");
        public static readonly SheetComponent SummaryCard = new SheetComponent("summaryCard");
        public static readonly SheetComponent StatusIcon = new SheetComponent("statusIcon");
        public static readonly SheetComponent GiftBox = new SheetComponent("giftBox");
        public static readonly SheetComponent HeroImage = new SheetComponent("heroImage");
        public static readonly SheetComponent GiftBoxImage = new SheetComponent("giftBoxImage");

        public bool Equals(SheetComponent other) => other != null && string.Equals(Name, other.Name, StringComparison.Ordinal);
        public override bool Equals(object obj) => Equals(obj as SheetComponent);
        public override int GetHashCode() => Name == null ? 0 : StringComparer.Ordinal.GetHashCode(Name);
        public override string ToString() => Name ?? "";
        public static bool operator ==(SheetComponent a, SheetComponent b) => ReferenceEquals(a, b) || (a is object && a.Equals(b));
        public static bool operator !=(SheetComponent a, SheetComponent b) => !(a == b);
    }

    // One component's look. Every field is optional; the tagged element's kind decides which apply, and the native SDK
    // drops a value that does not apply or is out of range with one warning.
    public sealed class SheetComponentStyle
    {
        // The background of a container, button or text, or a shape's fill.
        public SheetColor Fill;
        // Points, 0 to 999 (999 draws a capsule).
        public double? CornerRadius;
        // Points, 0 to 8.
        public double? BorderWidth;
        public SheetColor BorderColor;
        // Text and icon colour, inside the component too.
        public SheetColor ContentColor;
        public SheetFontRole? Font;
        // HTTPS image URLs with content-hashed names: a picture or icon, a conditional's true and false branch (such as
        // a selection check), and an image behind the element's content that covers its box.
        public string Image;
        public string TrueImage;
        public string FalseImage;
        public string BackgroundImage;
        // A Lottie file in place of the picture or icon; it wins over Image.
        public SheetAnimation Animation;
        // An entrance played once when the element appears.
        public SheetEnter Enter;
        // A repeating animation, in place of the variant's own.
        public SheetLoop Loop;
    }

    // One hex (#RRGGBB or #RRGGBBAA) for both schemes, or a light and a dark one. A string converts to one hex, so
    // Fill = "#F5C52B" works. A bad hex is dropped by the native SDK with one warning.
    public sealed class SheetColor
    {
        public string Light { get; }
        public string Dark { get; }

        public SheetColor(string hex) : this(hex, hex) { }

        public SheetColor(string light, string dark)
        {
            Light = light;
            Dark = dark;
        }

        public static implicit operator SheetColor(string hex) => hex == null ? null : new SheetColor(hex);
    }

    // Which of the two sheet fonts a component's text takes.
    public enum SheetFontRole { Heading, Body }

    // A Lottie file in place of an element's picture or icon. It plays when the game links Lottie natively
    // (lottie-ios 4.x, com.airbnb.android:lottie 5.2 or later) and reduce motion is off; otherwise FallbackImage draws.
    public sealed class SheetAnimation
    {
        // HTTPS Lottie JSON, at most 150 KB, with a content-hashed name. Required.
        public string Url;
        // The HTTPS still, drawn until the file is ready and whenever it cannot play. Required.
        public string FallbackImage;
        // Null loops; false plays once and holds the last frame.
        public bool? Loop;
        // Null plays; false draws the first frame.
        public bool? Autoplay;
        // Offer pictures for the file's placeholder images, by Lottie image asset id.
        public Dictionary<string, SheetAnimationImage> Images;
    }

    // Which of an offer's pictures fills a Lottie placeholder.
    public enum SheetOfferPicture { Logo, Hero, PrimaryCreative }

    // An offer picture drawn into a Lottie placeholder. Offer is the position in the sheet's offers, 0 to 9.
    public sealed class SheetAnimationImage
    {
        public int Offer;
        public SheetOfferPicture Picture;
    }

    // How a sheet motion eases.
    public enum SheetMotionCurve { Linear, EaseIn, EaseOut, EaseInOut }

    // A pose relative to the element's own; a null property holds the element's value. Scale 0 to 10, opacity 0 to 1,
    // rotation in degrees, offsets in points (setting one offset sets both, the other at 0).
    public sealed class SheetPose
    {
        public double? Scale;
        public double? Opacity;
        public double? Rotation;
        public double? OffsetX;
        public double? OffsetY;
    }

    // An entrance from From to the element's own pose over Duration seconds (above 0, at most 10), after Delay seconds
    // (0 to 10). Skipped under reduce motion. From is required.
    public sealed class SheetEnter
    {
        public SheetPose From;
        public double Duration;
        public double? Delay;
        // Null eases out.
        public SheetMotionCurve? Curve;
    }

    // One pose of a SheetLoop, At 0 to 1 through the cycle.
    public sealed class SheetKeyframe
    {
        public double At;
        public SheetPose Pose;
    }

    // A repeating animation: two or more keyframes from At 0 to At 1 over Duration seconds, after Delay seconds. After
    // RepeatCount cycles (1 to 1000) it holds the last keyframe; null runs forever. Under reduce motion it shows ReducedMotion.
    public sealed class SheetLoop
    {
        public double Duration;
        public List<SheetKeyframe> Keyframes = new List<SheetKeyframe>();
        // Null is linear, per segment.
        public SheetMotionCurve? Curve;
        // 0 to 1: how far into the cycle it starts.
        public double? PhaseOffset;
        public SheetPose ReducedMotion;
        public double? Delay;
        public int? RepeatCount;
    }

    // Your own prize for trying an offer. Grant it when the result's Claim.UserConfirmedCompletion is true.
    // Only Id leaves the device. A blank Id or Title means no prize.
    public sealed class PublisherReward
    {
        public string Id;
        public string Title;
        public string Detail;
        // An http or https image URL; anything else is ignored.
        public string IconUrl;
    }

    public enum TriggerIcon { Gift, AppIcon, None }

    public enum EncoreAlignment { Leading, Center, Trailing }

    public enum NotPresentedReason
    {
        None,
        NotConfigured,
        AlreadyPresenting,
        NoOffers,
        ExperimentControl,
        UseCaseUnavailable,
        Error,
        // Android only.
        NoForegroundActivity,
        // Android only.
        IapFirstDeclined,
        // iOS only.
        UnsupportedOS,
        // A value this version of the package does not know.
        Unknown,
        // The game turned the SDK off with Encore.IsEnabled = false: nothing was requested or shown. Android
        // and iOS; WebGL once the bundled web SDK has isEnabled (2.7.0); never in the Editor.
        // After Unknown so every released value keeps its number; add new reasons at the end.
        Disabled,
    }

    public enum AdvertiserOutcomeType { NotAttempted, Claimed, Verified, Cooldown, Failed, Unknown }

    public sealed class ClaimedOffer
    {
        // Null on Android, which identifies an offer by its campaign only.
        public string OfferId;
        public string CampaignId;
        public string AdvertiserName;
        // Carries the attribution. Keep it when the user claims.
        public string TransactionId;
        public bool UserConfirmedCompletion;
        // The attributable link the user opened. It completes the offer as this user, so keep it out of logs and analytics.
        // Null for demo offers and links with no transaction id. Android, iOS and WebGL.
        public string ClaimUrl;
    }

    public sealed class PresentationResult
    {
        public bool Presented;
        public NotPresentedReason Reason;
        public string ErrorType;
        public string ErrorMessage;
        public AdvertiserOutcomeType Advertiser;
        public ClaimedOffer Offer;
        public string Publisher;
        // The native dismiss reason wire value, for example "close_button".
        public string Dismissal;

        public ClaimedOffer Claim =>
            Advertiser == AdvertiserOutcomeType.Claimed || Advertiser == AdvertiserOutcomeType.Verified ? Offer : null;

        internal static PresentationResult NotPresentedBecause(NotPresentedReason reason, string message = null) =>
            new PresentationResult { Presented = false, Reason = reason, ErrorMessage = message };
    }

    public enum PlacementOutcomeKind
    {
        Presentation,
        // UnlockMode.Strict only. The SDK already refreshed entitlements.
        StrictUnlockVerified,
    }

    public sealed class PlacementOutcome
    {
        public PlacementOutcomeKind Kind;
        // Presentation only.
        public string PlacementId;
        // Presentation only.
        public PresentationResult Result;
        // StrictUnlockVerified only. Matches ClaimedOffer.TransactionId of the claim.
        public string TransactionId;
    }
}
