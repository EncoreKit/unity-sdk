using System;
using UnityEngine;

namespace EncoreKit.Internal
{
    // The custom claim's messages. Keys and values are the Flutter claim channel's (flutter-sdk lib/src/claim_channel.dart).
    internal static partial class Wire
    {
        [Serializable]
        private sealed class ClaimStateDto
        {
            public string kind;
            public int offerCount;
            public string[] logoUrls;
            public string advertiserName;
            public string logoUrl;
            public string perk;
            public long claimedAt;
            public long deadline;
            public int claimCount;
            public string transactionId;
        }

        [Serializable] private sealed class ClaimMessageDto { public string @event; public ClaimStateDto state; public ResultDto result; }

        internal struct ClaimMessage
        {
            // Set for a state message; Result for a result message.
            public ClaimState State;
            public PresentationResult Result;
        }

        // The Flutter create arguments, without the id.
        internal static string ClaimCreate(string placement, ClaimOptions o)
        {
            var json = new JsonWriter();
            json.Field("placement", placement);
            json.Field("useCase", UseCaseValue(o.UseCase));
            if (o.AutoOpenOff) json.Field("autoOpen", "off");
            if (o.PublisherReward != null) json.Raw("publisherReward", Reward(o.PublisherReward));
            json.Field("sheetHeadline", o.SheetHeadline);
            json.Field("sheetSubheadline", o.SheetSubheadline);
            return json.ToString();
        }

        // The key is always sent: {} has no id, so the bridges read it as no prize.
        internal static string ClaimPublisherReward(PublisherReward reward)
        {
            var json = new JsonWriter();
            json.Raw("publisherReward", reward == null ? "{}" : Reward(reward));
            return json.ToString();
        }

        // A missing key goes back to the sheet's own text, as Flutter's setSheetText with null.
        internal static string ClaimSheetText(string headline, string subheadline)
        {
            var json = new JsonWriter();
            json.Field("sheetHeadline", headline);
            json.Field("sheetSubheadline", subheadline);
            return json.ToString();
        }

        // "" lets the native SDK pick, as Flutter leaves the key out. JNI cannot infer a signature from null.
        internal static string ClaimTargetValue(ClaimTarget? target) => target switch
        {
            ClaimTarget.Button => "button",
            ClaimTarget.Card => "card",
            ClaimTarget.Arrow => "arrow",
            _ => "",
        };

        // Null for a message this version does not know. A state it cannot read is hidden: when in doubt, draw nothing.
        internal static ClaimMessage? ParseClaimMessage(string json)
        {
            ClaimMessageDto dto;
            try
            {
                dto = JsonUtility.FromJson<ClaimMessageDto>(json);
            }
            catch (Exception)
            {
                return null;
            }
            if (dto?.@event == "state") return new ClaimMessage { State = ClaimStateFrom(dto.state) };
            if (dto?.@event == "result") return new ClaimMessage { Result = FromDto(dto.result) };
            return null;
        }

        private static ClaimState ClaimStateFrom(ClaimStateDto s)
        {
            if (s == null) return ClaimState.Hidden;
            try
            {
                switch (s.kind)
                {
                    case "gifts":
                    case "thanks":
                        if (s.offerCount < 0 || s.logoUrls == null) return ClaimState.Hidden;
                        var kind = s.kind == "gifts" ? ClaimStateKind.Gifts : ClaimStateKind.Thanks;
                        return ClaimState.Offers(kind, s.offerCount, Array.AsReadOnly((string[])s.logoUrls.Clone()));
                    case "reminder":
                        // JsonUtility reads a missing key as empty or 0, so those stand for missing.
                        if (string.IsNullOrEmpty(s.transactionId) || s.claimedAt <= 0 || s.deadline <= 0) return ClaimState.Hidden;
                        return ClaimState.Reminding(s.advertiserName ?? "", OrNull(s.logoUrl), OrNull(s.perk),
                            DateTimeOffset.FromUnixTimeMilliseconds(s.claimedAt), DateTimeOffset.FromUnixTimeMilliseconds(s.deadline),
                            s.claimCount, s.transactionId);
                    default:
                        return ClaimState.Hidden;
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // A date past what DateTimeOffset holds.
                return ClaimState.Hidden;
            }
        }
    }
}
