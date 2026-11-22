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

        [Serializable] private sealed class OfferDto { public string offerId; public string campaignId; public string advertiserName; public string transactionId; public bool userConfirmedCompletion; public string claimUrl; }
        [Serializable] private sealed class AdvertiserDto { public string type; public string errorType; public string errorMessage; public OfferDto offer; }
        [Serializable] private sealed class ResultDto { public string type; public string reason; public string errorType; public string errorMessage; public string publisher; public string dismissal; public AdvertiserDto advertiser; }
        [Serializable] private sealed class OutcomeDto { public string type; public string placementId; public ResultDto result; public string transactionId; }
        [Serializable] private sealed class GiftsButtonDto { public string @event; public float height; public bool showing; public ResultDto result; }

        internal struct GiftsButtonMessage
        {
            public bool IsHeight;
            public float Height;
            public bool IsShowing;
            public bool Showing;
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
                if (dto?.@event == "showing") return new GiftsButtonMessage { IsShowing = true, Showing = dto.showing };
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
            json.Field("sheetHeadline", o.SheetHeadline);
            json.Field("sheetSubheadline", o.SheetSubheadline);
            if (o.Values != null) json.Raw("values", StringMap(o.Values));
            json.Field("icon", IconValue(o.Icon));
            json.Field("alignment", AlignmentValue(o.Alignment));
            if (o.Appearance != null) json.Raw("appearance", Appearance(o.Appearance, forTrigger: true));
            if (o.PublisherReward != null) json.Raw("publisherReward", Reward(o.PublisherReward));
            if (o is GiftsButtonOptions gifts && gifts.HostState != null) json.Raw("hostState", HostState(gifts.HostState));
            return json.ToString();
        }

        // The native GiftsButtonHostState's names. Steps cross beside the progress, so the native look keeps their exact ratio.
        private static string HostState(GiftsButtonHostState state)
        {
            var json = new JsonWriter();
            json.Field("status", state.Status switch
            {
                GiftsButtonHostStatus.Ready => "ready",
                GiftsButtonHostStatus.Done => "done",
                _ => "locked",
            });
            // As the float was written (0.7f is 0.7), as Android reads the progress.
            json.Number("progress", state.Progress);
            if (state.Completed.HasValue && state.Total.HasValue)
            {
                json.Raw("completed", state.Completed.Value.ToString(CultureInfo.InvariantCulture));
                json.Raw("total", state.Total.Value.ToString(CultureInfo.InvariantCulture));
            }
            return json.ToString();
        }

        // The key is always sent: {} has no status, so the bridges read it as no host state.
        internal static string GiftsButtonHostStateUpdate(GiftsButtonHostState state)
        {
            var json = new JsonWriter();
            json.Raw("hostState", state == null ? "{}" : HostState(state));
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
            json.Raw("appearance", appearance == null ? "{}" : Appearance(appearance, forTrigger: true));
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

        // The one gate: a trigger takes the look fields and no sheet, a placement the sheet and no look fields, as
        // natively. The skipped side writes no JSON and logs no warnings.
        private static string Appearance(AppearanceConfig a, bool forTrigger)
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
            if (forTrigger)
            {
                json.Field("fontFamily", Font(a.FontFamily));
                json.Field("countdownFontFamily", Font(a.CountdownFontFamily));
                json.Number("cornerRadius", Finite(a.CornerRadius));
                // An unknown value is unset, as for the other enums here.
                json.Field("shadow", a.Shadow switch
                {
                    EncoreShadow.Flat => "flat",
                    EncoreShadow.Soft => "soft",
                    EncoreShadow.Strong => "strong",
                    _ => null,
                });
                json.Number("borderWidth", Finite(a.BorderWidth));
                json.Field("borderColor", a.BorderColor);
                json.Field("backgroundImage", Font(a.BackgroundImage));
                // JSON cannot carry a non-finite side, so it drops the insets, as natively. The SDK range-checks the rest.
                if (a.BackgroundImageCapInsets is EncoreEdgeInsets i
                    && Finite(i.Top).HasValue && Finite(i.Left).HasValue && Finite(i.Bottom).HasValue && Finite(i.Right).HasValue)
                {
                    var insets = new JsonWriter();
                    insets.Number("top", i.Top);
                    insets.Number("left", i.Left);
                    insets.Number("bottom", i.Bottom);
                    insets.Number("right", i.Right);
                    json.Raw("backgroundImageCapInsets", insets.ToString());
                }
                json.Field("iconImage", Font(a.IconImage));
            }
            if (!forTrigger && a.Sheet != null)
            {
                var sheet = new JsonWriter();
                sheet.Field("headingFontFamily", Font(a.Sheet.HeadingFontFamily));
                sheet.Field("bodyFontFamily", Font(a.Sheet.BodyFontFamily));
                var components = Components(a.Sheet.Components);
                if (components != null) sheet.Raw("components", components);
                if (!sheet.IsEmpty) json.Raw("sheet", sheet.ToString());
            }
            return json.ToString();
        }

        // The component styles, keyed by trimmed name in ordinal order; null when none is left. A blank URL is unset, as
        // a blank font is. Every other value C# cannot send is dropped with one warning; ranges are the native SDK's.
        private static string Components(Dictionary<SheetComponent, SheetComponentStyle> components)
        {
            if (components == null || components.Count == 0) return null;
            var named = new SortedDictionary<string, KeyValuePair<string, SheetComponentStyle>>(StringComparer.Ordinal);
            foreach (var pair in components)
            {
                var raw = pair.Key?.Name;
                var name = raw?.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    SheetWarn($"\"{raw}\"", "is a blank name");
                    continue;
                }
                if (pair.Value == null)
                {
                    SheetWarn(name, "has a null style");
                    continue;
                }
                // Two names that trim to one: the exact name wins, whatever the dictionary order.
                if (named.TryGetValue(name, out var kept))
                {
                    var exact = raw == name;
                    SheetWarn($"\"{(exact ? kept.Key : raw)}\"", $"is {name} again once trimmed");
                    if (!exact) continue;
                }
                named[name] = new KeyValuePair<string, SheetComponentStyle>(raw, pair.Value);
            }
            if (named.Count == 0) return null;
            var json = new JsonWriter();
            foreach (var pair in named) json.Raw(pair.Key, ComponentStyle(pair.Key, pair.Value.Value));
            return json.ToString();
        }

        private static void SheetWarn(string path, string reason) =>
            Log.Warn($"appearance.sheet.components {path} {reason}; ignored.");

        private static string ComponentStyle(string name, SheetComponentStyle s)
        {
            var json = new JsonWriter();
            SheetColorField(json, "fill", s.Fill, name);
            NumberField(json, "cornerRadius", s.CornerRadius, name);
            NumberField(json, "borderWidth", s.BorderWidth, name);
            SheetColorField(json, "borderColor", s.BorderColor, name);
            SheetColorField(json, "contentColor", s.ContentColor, name);
            if (s.Font.HasValue)
            {
                var font = s.Font.Value switch { SheetFontRole.Heading => "heading", SheetFontRole.Body => "body", _ => null };
                if (font == null) SheetWarn(name + ".font", "is not Heading or Body");
                json.Field("font", font);
            }
            json.Field("image", Url(s.Image));
            json.Field("trueImage", Url(s.TrueImage));
            json.Field("falseImage", Url(s.FalseImage));
            json.Field("backgroundImage", Url(s.BackgroundImage));
            if (s.Animation != null) Optional(json, "animation", SheetAnimationJson(name + ".animation", s.Animation), name, "needs a Url and a FallbackImage");
            if (s.Enter != null) Optional(json, "enter", SheetEnterJson(name + ".enter", s.Enter), name, "needs a From pose, a finite Duration and a known Curve");
            if (s.Loop != null) Optional(json, "loop", SheetLoopJson(name + ".loop", s.Loop), name, "needs a finite Duration, keyframes with a finite At and a Pose, and a known Curve");
            return json.ToString();
        }

        private static void Optional(JsonWriter json, string key, string value, string name, string reason)
        {
            if (value == null) SheetWarn(name + "." + key, reason);
            else json.Raw(key, value);
        }

        // One hex for both schemes crosses as a string, as the natives write it. A null half drops the colour.
        private static void SheetColorField(JsonWriter json, string key, SheetColor color, string path)
        {
            if (color == null) return;
            if (color.Light == null || color.Dark == null)
            {
                SheetWarn(path + "." + key, "needs a light and a dark hex");
                return;
            }
            if (color.Light == color.Dark)
            {
                json.Field(key, color.Light);
                return;
            }
            var pair = new JsonWriter();
            pair.Field("light", color.Light);
            pair.Field("dark", color.Dark);
            json.Raw(key, pair.ToString());
        }

        // Invariant culture, so a game on a comma-decimal locale still writes JSON. Null for NaN and infinity.
        private static string Number(double? value) =>
            value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value)
                ? value.Value.ToString("R", CultureInfo.InvariantCulture)
                : null;

        private static void NumberField(JsonWriter json, string key, double? value, string path)
        {
            if (!value.HasValue) return;
            var number = Number(value);
            if (number == null) SheetWarn(path + "." + key, "is not a finite number");
            else json.Raw(key, number);
        }

        private static string Url(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string SheetAnimationJson(string path, SheetAnimation a)
        {
            var url = Url(a.Url);
            var still = Url(a.FallbackImage);
            if (url == null || still == null) return null;
            var json = new JsonWriter();
            json.Field("url", url);
            json.Field("fallbackImage", still);
            if (a.Loop.HasValue) json.Raw("loop", a.Loop.Value ? "true" : "false");
            if (a.Autoplay.HasValue) json.Raw("autoplay", a.Autoplay.Value ? "true" : "false");
            if (a.Images != null)
            {
                var images = new JsonWriter();
                var ids = new List<string>(a.Images.Keys);
                ids.Sort(StringComparer.Ordinal);
                foreach (var id in ids)
                {
                    var slot = a.Images[id];
                    if (slot == null)
                    {
                        SheetWarn(path + ".images." + id, "is null");
                        continue;
                    }
                    var picture = slot.Picture switch
                    {
                        SheetOfferPicture.Logo => "logo",
                        SheetOfferPicture.Hero => "hero",
                        SheetOfferPicture.PrimaryCreative => "primaryCreative",
                        _ => null,
                    };
                    if (picture == null)
                    {
                        SheetWarn(path + ".images." + id, "needs a known Picture");
                        continue;
                    }
                    var one = new JsonWriter();
                    one.Raw("offer", slot.Offer.ToString(CultureInfo.InvariantCulture));
                    one.Field("picture", picture);
                    images.Raw(id, one.ToString());
                }
                if (!images.IsEmpty) json.Raw("images", images.ToString());
            }
            return json.ToString();
        }

        private static string Pose(string path, SheetPose p)
        {
            var json = new JsonWriter();
            NumberField(json, "scale", p.Scale, path);
            NumberField(json, "opacity", p.Opacity, path);
            NumberField(json, "rotation", p.Rotation, path);
            NumberField(json, "offsetX", p.OffsetX, path);
            NumberField(json, "offsetY", p.OffsetY, path);
            return json.ToString();
        }

        // An empty string for absent (the SDK's default), null for a value the SDK does not know, which drops the motion.
        private static string Curve(SheetMotionCurve? curve) => !curve.HasValue ? "" : curve.Value switch
        {
            SheetMotionCurve.Linear => "linear",
            SheetMotionCurve.EaseIn => "easeIn",
            SheetMotionCurve.EaseOut => "easeOut",
            SheetMotionCurve.EaseInOut => "easeInOut",
            _ => null,
        };

        private static string SheetEnterJson(string path, SheetEnter e)
        {
            var duration = Number(e.Duration);
            var curve = Curve(e.Curve);
            if (e.From == null || duration == null || curve == null) return null;
            var json = new JsonWriter();
            json.Raw("from", Pose(path + ".from", e.From));
            json.Raw("duration", duration);
            NumberField(json, "delay", e.Delay, path);
            if (curve.Length > 0) json.Field("curve", curve);
            return json.ToString();
        }

        // Checked before anything is written, so a loop that is dropped warns once, not once per keyframe value too.
        private static string SheetLoopJson(string path, SheetLoop l)
        {
            var duration = Number(l.Duration);
            var curve = Curve(l.Curve);
            if (duration == null || l.Keyframes == null || curve == null) return null;
            foreach (var frame in l.Keyframes)
            {
                if (frame == null || Number(frame.At) == null || frame.Pose == null) return null;
            }
            var frames = new StringBuilder("[");
            for (var i = 0; i < l.Keyframes.Count; i++)
            {
                var one = new JsonWriter();
                one.Raw("at", Number(l.Keyframes[i].At));
                one.Raw("pose", Pose($"{path}.keyframes[{i}].pose", l.Keyframes[i].Pose));
                if (i > 0) frames.Append(',');
                frames.Append(one);
            }
            var json = new JsonWriter();
            json.Raw("duration", duration);
            json.Raw("keyframes", frames.Append(']').ToString());
            if (curve.Length > 0) json.Field("curve", curve);
            NumberField(json, "phaseOffset", l.PhaseOffset, path);
            if (l.ReducedMotion != null) json.Raw("reducedMotion", Pose(path + ".reducedMotion", l.ReducedMotion));
            NumberField(json, "delay", l.Delay, path);
            if (l.RepeatCount.HasValue) json.Raw("repeatCount", l.RepeatCount.Value.ToString(CultureInfo.InvariantCulture));
            return json.ToString();
        }

        // A blank font or image name is none.
        private static string Font(string family) => string.IsNullOrWhiteSpace(family) ? null : family.Trim();

        // JSON cannot carry NaN or an infinity, so either is none. The native SDK clamps the range.
        private static float? Finite(float? value) =>
            value.HasValue && !float.IsNaN(value.Value) && !float.IsInfinity(value.Value) ? value : null;

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

        // {"sheetText":{...}}: a missing key inside goes back to the sheet's own text.
        internal static string TriggerSheetTextUpdate(string headline, string subheadline)
        {
            var text = new JsonWriter();
            text.Field("sheetHeadline", headline);
            text.Field("sheetSubheadline", subheadline);
            var json = new JsonWriter();
            json.Raw("sheetText", text.ToString());
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
                    ClaimUrl = OrNull(advertiser.offer.claimUrl),
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
            "disabled" => NotPresentedReason.Disabled,
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
            if (appearance != null) json.Raw("appearance", Appearance(appearance, forTrigger: false));
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

            public void Number(string key, float? value)
            {
                if (key == null || !value.HasValue) return;
                Raw(key, value.Value.ToString("R", CultureInfo.InvariantCulture));
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
