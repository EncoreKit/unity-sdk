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
        // The reward sheet's fonts. Triggers ignore it. Null keeps the sheet's own.
        public SheetAppearance Sheet;
    }

    // Fonts for the reward sheet, by the name the app registered natively: an iOS font name listed under UIAppFonts,
    // an Android res/font resource name. A Unity font asset is not a native font. Blank or unknown draws the system font.
    public sealed class SheetAppearance
    {
        // The text the sheet's style in the Encore portal marks as heading, such as the title.
        public string HeadingFontFamily;
        // Every other text on the sheet.
        public string BodyFontFamily;
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
