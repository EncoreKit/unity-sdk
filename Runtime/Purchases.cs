using System.Threading.Tasks;

namespace EncoreKit
{
    // What the game's purchase code reports back. Throw (or fault the task) for a real failure instead.
    public enum PurchaseResult { Purchased, Cancelled, Pending }

    // Encore's offer flow asks the game to buy this product.
    public sealed class PurchaseRequest
    {
        public string ProductId;
        // Null when the purchase did not come from a named placement.
        public string PlacementId;
        // The offer to apply: an App Store promotional offer on iOS, a Google Play offer on Android.
        public string PromoOfferId;
        // Android only: the Google Play base plan to buy.
        public string BasePlanId;
    }

    // Runs a purchase through the game's own billing code. Pass it to Encore.Configure; it lives for the process.
    // Called on the Unity main thread. Encore owns the sheet, analytics and attribution around the call.
    public interface IEncorePurchaseController
    {
        Task<PurchaseResult> Purchase(PurchaseRequest request);
    }

    // When the SDK grants access after an offer flow. Set once, at Configure.
    public enum UnlockMode
    {
        // Grant when the user comes back from the offer. The default.
        Optimistic,
        // Wait for the advertiser's postback; a late verification arrives as PlacementOutcomeKind.StrictUnlockVerified.
        Strict,
    }

    public sealed class EncoreOptions
    {
        public LogLevel LogLevel = LogLevel.None;
        public UnlockMode Unlock = UnlockMode.Optimistic;
        // Links the store's purchases to the user for lift measurement. Android and iOS; WebGL has no store.
        public bool TransactionLinking = true;
    }

    // Android only: the IAP product Encore's remote config offers now.
    public sealed class IapEntitlement
    {
        public string ProductId;
        public string OfferId;
        public string BasePlanId;
    }
}
