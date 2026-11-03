using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace EncoreKit.Internal
{
    // The bridge wire format. Keys and values match the Flutter and React Native bridges verbatim.
    internal static partial class Wire
    {
        internal const string RewardUsers = "post-action-reward";
        internal const string ReduceChurn = "churn-intervention";

        [Serializable] private sealed class OfferDto { public string offerId; public string campaignId; public string advertiserName; public string transactionId; public bool userConfirmedCompletion; }
        [Serializable] private sealed class AdvertiserDto { public string type; public string errorType; public string errorMessage; public OfferDto offer; }
        [Serializable] private sealed class ResultDto { public string type; public string reason; public string errorType; public string errorMessage; public string publisher; public string dismissal; public AdvertiserDto advertiser; }
        [Serializable] private sealed class OutcomeDto { public string type; public string placementId; public ResultDto result; public string transactionId; }
        [Serializable] private sealed class GiftsButtonDto { public string @event; public float height; public ResultDto result; }

        internal struct GiftsButtonMessage
        {
            public bool IsHeight;
            public float Height;
            public PresentationResult Result;
        }

        // Null for a message this version does not know. A height JsonUtility cannot read throws, so it is null too.
        internal static GiftsButtonMessage? ParseGiftsButtonMessage(string json)
        {
            try
            {
                var dto = JsonUtility.FromJson<GiftsButtonDto>(json);
                if (dto?.@event == "height") return new GiftsButtonMessage { IsHeight = true, Height = Math.Max(0f, dto.height) };
                if (dto?.@event == "result") return new GiftsButtonMessage { Result = FromDto(dto.result) };
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The create options of any trigger view: the gift button, the claim reminder, or another id.
        internal static string TriggerOptions(string triggerId, string placement, TriggerOptions o)
        {
            var json = new JsonWriter();
            json.Field("triggerId", triggerId);
            json.Field("placement", placement);
            json.Field("useCase", UseCaseValue(o.UseCase));
            if (o.AutoOpenOff) json.Field("autoOpen", "off");
            json.Field("headline", o.Headline);
            json.Field("subheadline", o.Subheadline);
            json.Field("cta", o.Cta);
            if (o.Values != null) json.Raw("values", StringMap(o.Values));
            json.Field("icon", IconValue(o.Icon));
            json.Field("alignment", AlignmentValue(o.Alignment));
            if (o.Appearance != null) json.Raw("appearance", Appearance(o.Appearance));
            if (o.PublisherReward != null) json.Raw("publisherReward", Reward(o.PublisherReward));
            return json.ToString();
        }

        internal static string GiftsButtonAlignmentUpdate(EncoreAlignment alignment)
        {
            var json = new JsonWriter();
            json.Field("alignment", AlignmentValue(alignment));
            return json.ToString();
        }

        // The key is always sent: {} clears the colours, as a null appearance does natively.
        internal static string GiftsButtonAppearanceUpdate(AppearanceConfig appearance)
        {
            var json = new JsonWriter();
            json.Raw("appearance", appearance == null ? "{}" : Appearance(appearance));
            return json.ToString();
        }

        // The key is always sent: {} has no id, so the bridges read it as no prize.
        internal static string GiftsButtonPublisherRewardUpdate(PublisherReward reward)
        {
            var json = new JsonWriter();
            json.Raw("publisherReward", reward == null ? "{}" : Reward(reward));
            return json.ToString();
        }

        // Keys and values as the Flutter bridge's.
        private static string IconValue(TriggerIcon icon) => icon switch
        {
            TriggerIcon.AppIcon => "appIcon",
            TriggerIcon.None => "none",
            _ => "gift",
        };

        private static string AlignmentValue(EncoreAlignment alignment) => alignment switch
        {
            EncoreAlignment.Leading => "leading",
            EncoreAlignment.Trailing => "trailing",
            _ => "center",
        };

        private static string Appearance(AppearanceConfig a)
        {
            var json = new JsonWriter();
            json.Field("accentColor", a.AccentColor);
            json.Field("backgroundColor", a.BackgroundColor);
            json.Field("textColor", a.TextColor);
            json.Field("secondaryTextColor", a.SecondaryTextColor);
            json.Field("trackColor", a.TrackColor);
            json.Field("onAccentColor", a.OnAccentColor);
            if (a.ColorScheme.HasValue)
            {
                json.Field("colorScheme", a.ColorScheme.Value switch
                {
                    ColorScheme.Light => "light",
                    ColorScheme.Dark => "dark",
                    _ => "system",
                });
            }
            json.Field("backgroundEndColor", a.BackgroundEndColor);
            return json.ToString();
        }

        private static string Reward(PublisherReward r)
        {
            var json = new JsonWriter();
            json.Field("id", r.Id);
            json.Field("title", r.Title);
            json.Field("detail", r.Detail);
            json.Field("iconUrl", r.IconUrl);
            return json.ToString();
        }

        // {"text":{...}}: a missing key inside clears that slot to the look's own text.
        internal static string GiftsButtonTextUpdate(string headline, string subheadline, string cta)
        {
            var text = new JsonWriter();
            text.Field("headline", headline);
            text.Field("subheadline", subheadline);
            text.Field("cta", cta);
            var json = new JsonWriter();
            json.Raw("text", text.ToString());
            return json.ToString();
        }

        internal static string GiftsButtonValuesUpdate(IDictionary<string, string> values)
        {
            var json = new JsonWriter();
            json.Raw("values", StringMap(values));
            return json.ToString();
        }

        private static string StringMap(IDictionary<string, string> map)
        {
            var json = new JsonWriter();
            foreach (var pair in map) json.Field(pair.Key, pair.Value);
            return json.ToString();
        }

        internal static string UseCaseValue(UseCase useCase) => useCase switch
        {
            UseCase.RewardUsers => RewardUsers,
            UseCase.ReduceChurn => ReduceChurn,
            _ => throw new ArgumentOutOfRangeException(nameof(useCase)),
        };

        internal static PresentationResult ParseResult(string json)
        {
            try
            {
                return FromDto(JsonUtility.FromJson<ResultDto>(json));
            }
            catch (Exception e)
            {
                return PresentationResult.NotPresentedBecause(NotPresentedReason.Error, "Unreadable native result: " + e.Message);
            }
        }

        // Null for an outcome this version does not know, or a strict verification with no transaction id.
        internal static PlacementOutcome ParseOutcome(string json)
        {
            try
            {
                var dto = JsonUtility.FromJson<OutcomeDto>(json);
                if (dto?.type == "strictUnlockVerified")
                {
                    var transactionId = OrNull(dto.transactionId);
                    if (transactionId == null) return null;
                    return new PlacementOutcome { Kind = PlacementOutcomeKind.StrictUnlockVerified, TransactionId = transactionId };
                }
                if (dto == null || dto.type != "presentation") return null;
                return new PlacementOutcome { PlacementId = OrNull(dto.placementId), Result = FromDto(dto.result) };
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static PresentationResult FromDto(ResultDto dto)
        {
            if (dto == null) return PresentationResult.NotPresentedBecause(NotPresentedReason.Error, "Empty native result");
            if (dto.type != "presented")
            {
                return new PresentationResult
                {
                    Presented = false,
                    Reason = Reason(dto.reason),
                    ErrorType = OrNull(dto.errorType),
                    ErrorMessage = OrNull(dto.errorMessage),
                };
            }
            var advertiser = dto.advertiser ?? new AdvertiserDto();
            var hasOffer = advertiser.offer != null && !string.IsNullOrEmpty(advertiser.offer.campaignId);
            return new PresentationResult
            {
                Presented = true,
                Reason = NotPresentedReason.None,
                Advertiser = AdvertiserType(advertiser.type),
                ErrorType = OrNull(advertiser.errorType),
                ErrorMessage = OrNull(advertiser.errorMessage),
                Offer = hasOffer ? new ClaimedOffer
                {
                    OfferId = OrNull(advertiser.offer.offerId),
                    CampaignId = advertiser.offer.campaignId,
                    AdvertiserName = OrNull(advertiser.offer.advertiserName),
                    TransactionId = OrNull(advertiser.offer.transactionId),
                    UserConfirmedCompletion = advertiser.offer.userConfirmedCompletion,
                } : null,
                Publisher = OrNull(dto.publisher),
                Dismissal = OrNull(dto.dismissal),
            };
        }

        private static NotPresentedReason Reason(string value) => value switch
        {
            "not_configured" => NotPresentedReason.NotConfigured,
            "already_presenting" => NotPresentedReason.AlreadyPresenting,
            "no_offer_available" => NotPresentedReason.NoOffers,
            "experiment_control" => NotPresentedReason.ExperimentControl,
            "use_case_unavailable" => NotPresentedReason.UseCaseUnavailable,
            "error" => NotPresentedReason.Error,
            "no_foreground" => NotPresentedReason.NoForegroundActivity,
            "iap_first_declined" => NotPresentedReason.IapFirstDeclined,
            "unsupported_ios" => NotPresentedReason.UnsupportedOS,
            _ => NotPresentedReason.Unknown,
        };

        private static AdvertiserOutcomeType AdvertiserType(string value) => value switch
        {
            "notAttempted" => AdvertiserOutcomeType.NotAttempted,
            "claimed" => AdvertiserOutcomeType.Claimed,
            "verified" => AdvertiserOutcomeType.Verified,
            "cooldown" => AdvertiserOutcomeType.Cooldown,
            "failed" => AdvertiserOutcomeType.Failed,
            _ => AdvertiserOutcomeType.Unknown,
        };

        // JsonUtility reads a missing string as empty; the public models use null for "absent".
        private static string OrNull(string value) => string.IsNullOrEmpty(value) ? null : value;

        internal static string PlacementRequest(
            string placementId, UseCase useCase, string headline, string subheadline,
            AppearanceConfig appearance = null, PublisherReward publisherReward = null,
            string cta = null, string decline = null, string badge = null)
        {
            var json = new JsonWriter();
            json.Field("placementId", placementId);
            json.Field("useCase", UseCaseValue(useCase));
            json.Field("headline", headline);
            json.Field("subheadline", subheadline);
            json.Field("cta", cta);
            json.Field("decline", decline);
            json.Field("badge", badge);
            if (appearance != null) json.Raw("appearance", Appearance(appearance));
            if (publisherReward != null) json.Raw("publisherReward", Reward(publisherReward));
            return json.ToString();
        }

        internal static string Attributes(UserAttributes a)
        {
            if (a == null) return null;
            var json = new JsonWriter();
            json.Field("email", a.Email);
            json.Field("firstName", a.FirstName);
            json.Field("lastName", a.LastName);
            json.Field("phoneNumber", a.PhoneNumber);
            json.Field("postalCode", a.PostalCode);
            json.Field("city", a.City);
            json.Field("state", a.State);
            json.Field("countryCode", a.CountryCode);
            json.Field("latitude", a.Latitude);
            json.Field("longitude", a.Longitude);
            if (a.Age.HasValue) json.Raw("age", a.Age.Value.ToString(CultureInfo.InvariantCulture));
            json.Field("dateOfBirth", a.DateOfBirth);
            json.Field("gender", a.Gender);
            json.Field("subscriptionTier", a.SubscriptionTier);
            json.Field("monthsSubscribed", a.MonthsSubscribed);
            json.Field("billingCycle", a.BillingCycle);
            json.Field("lastPaymentAmount", a.LastPaymentAmount);
            json.Field("lastActiveDate", a.LastActiveDate);
            json.Field("totalSessions", a.TotalSessions);
            if (a.Custom != null && a.Custom.Count > 0)
            {
                var custom = new JsonWriter();
                foreach (var pair in a.Custom) custom.Field(pair.Key, pair.Value);
                json.Raw("custom", custom.ToString());
            }
            // No fields means "no change". An empty object would replace the stored attributes on Android.
            return json.IsEmpty ? null : json.ToString();
        }

        // Writes one flat object; null values are left out.
        private sealed class JsonWriter
        {
            private readonly StringBuilder _sb = new StringBuilder("{");
            private bool _first = true;

            public bool IsEmpty => _first;

            public void Field(string key, string value)
            {
                if (key == null || value == null) return;
                Raw(key, Quote(value));
            }

            public void Raw(string key, string rawJson)
            {
                if (!_first) _sb.Append(',');
                _first = false;
                _sb.Append(Quote(key)).Append(':').Append(rawJson);
            }

            public override string ToString() => _sb.ToString() + "}";

            private static string Quote(string s)
            {
                var sb = new StringBuilder(s.Length + 2).Append('"');
                foreach (var c in s)
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            else sb.Append(c);
                            break;
                    }
                }
                return sb.Append('"').ToString();
            }
        }
    }
}
