using System;
using System.Globalization;
using UnityEngine;

namespace EncoreKit.Internal
{
    // The purchase controller, configure options and entitlement messages. Keys follow the Flutter and React Native
    // bridges where they have them, and the Swift SDK's names where no wrapper does (entitlements).
    internal static partial class Wire
    {
        [Serializable] private sealed class PurchaseRequestDto { public long requestId; public string productId; public string placementId; public string promoOfferId; public string basePlanId; }
        [Serializable] private sealed class ActiveDto { public bool active; }
        [Serializable] private sealed class HasOffersDto { public bool hasOffers; }
        [Serializable] private sealed class CallResultDto { public bool success; public string errorType; public string errorMessage; }
        [Serializable] private sealed class IapEntitlementDto { public string productId; public string offerId; public string basePlanId; }

        internal static string ConfigureOptions(EncoreOptions options, bool hasPurchaseController)
        {
            var json = new JsonWriter();
            json.Field("logLevel", options.LogLevel.ToString().ToUpperInvariant());
            json.Field("unlock", options.Unlock == UnlockMode.Strict ? "strict" : "optimistic");
            json.Raw("transactionLinking", options.TransactionLinking ? "true" : "false");
            json.Raw("hasPurchaseController", hasPurchaseController ? "true" : "false");
            return json.ToString();
        }

        // Null for a request with no product, which no controller could buy.
        internal static (long Id, PurchaseRequest Request)? ParsePurchaseRequest(string json)
        {
            try
            {
                var dto = JsonUtility.FromJson<PurchaseRequestDto>(json);
                if (dto == null || string.IsNullOrEmpty(dto.productId)) return null;
                return (dto.requestId, new PurchaseRequest
                {
                    ProductId = dto.productId,
                    PlacementId = OrNull(dto.placementId),
                    PromoOfferId = OrNull(dto.promoOfferId),
                    BasePlanId = OrNull(dto.basePlanId),
                });
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The id of a request that cannot be read, so the bridge can still be answered with a failure.
        internal static long PurchaseRequestId(string json)
        {
            try { return JsonUtility.FromJson<PurchaseRequestDto>(json)?.requestId ?? 0; }
            catch (Exception) { return 0; }
        }

        internal static string PurchaseReply(PurchaseResult result)
        {
            var json = new JsonWriter();
            json.Field("result", result switch
            {
                PurchaseResult.Purchased => "purchased",
                PurchaseResult.Cancelled => "cancelled",
                PurchaseResult.Pending => "pending",
                _ => throw new ArgumentOutOfRangeException(nameof(result)),
            });
            return json.ToString();
        }

        // The native SDK records a failure as PublisherOutcome failed, never as a cancellation.
        internal static string PurchaseFailure(string message)
        {
            var json = new JsonWriter();
            json.Field("error", string.IsNullOrEmpty(message) ? "Purchase failed in the C# purchase controller" : message);
            return json.ToString();
        }

        // Throws for a type or unit this version does not know, or a value JSON cannot hold, so a query never asks about
        // another grant.
        internal static string EntitlementQuery(Entitlement entitlement, EntitlementScope scope)
        {
            var json = new JsonWriter();
            json.Field("type", entitlement.Type switch
            {
                EntitlementType.FreeTrial => "freeTrial",
                EntitlementType.Discount => "discount",
                EntitlementType.Credit => "credit",
                _ => throw new ArgumentOutOfRangeException(nameof(entitlement)),
            });
            if (entitlement.Value is double v && (double.IsNaN(v) || double.IsInfinity(v))) throw new ArgumentOutOfRangeException(nameof(entitlement));
            if (entitlement.Value.HasValue) json.Raw("value", entitlement.Value.Value.ToString("R", CultureInfo.InvariantCulture));
            if (entitlement.Unit.HasValue)
            {
                json.Field("unit", entitlement.Unit.Value switch
                {
                    EntitlementUnit.Months => "months",
                    EntitlementUnit.Days => "days",
                    EntitlementUnit.Percent => "percent",
                    EntitlementUnit.Dollars => "dollars",
                    _ => throw new ArgumentOutOfRangeException(nameof(entitlement)),
                });
            }
            json.Field("scope", scope == EntitlementScope.Verified ? "verified" : "all");
            return json.ToString();
        }

        // Anything unreadable is not active.
        internal static bool ParseActive(string json)
        {
            try { return JsonUtility.FromJson<ActiveDto>(json)?.active ?? false; }
            catch (Exception) { return false; }
        }

        // Anything unreadable has no offers.
        internal static bool ParseHasOffers(string json)
        {
            try { return JsonUtility.FromJson<HasOffersDto>(json)?.hasOffers ?? false; }
            catch (Exception) { return false; }
        }

        internal static EncoreCallResult ParseCallResult(string json)
        {
            try
            {
                var dto = JsonUtility.FromJson<CallResultDto>(json);
                if (dto == null) return Failure("Empty native result");
                return new EncoreCallResult { Success = dto.success, ErrorType = OrNull(dto.errorType), ErrorMessage = OrNull(dto.errorMessage) };
            }
            catch (Exception e)
            {
                return Failure("Unreadable native result: " + e.Message);
            }
        }

        internal static EncoreCallResult Failure(string message, string errorType = "domain_error") =>
            new EncoreCallResult { Success = false, ErrorType = errorType, ErrorMessage = message };

        // Null when no IAP product is configured, or the JSON has no product.
        internal static IapEntitlement ParseIapEntitlement(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var dto = JsonUtility.FromJson<IapEntitlementDto>(json);
                if (dto == null || string.IsNullOrEmpty(dto.productId)) return null;
                return new IapEntitlement { ProductId = dto.productId, OfferId = OrNull(dto.offerId), BasePlanId = OrNull(dto.basePlanId) };
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
