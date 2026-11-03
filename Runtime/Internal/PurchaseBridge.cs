using System;
using System.Threading.Tasks;

namespace EncoreKit.Internal
{
    // Runs the game's purchase controller for each native purchase request, and always answers the bridge.
    internal static class PurchaseBridge
    {
        internal static IEncorePurchaseController Controller;

        internal static void OnRequest(IEncorePlatform platform, string json) => MainThread.Post(() => _ = Handle(platform, json));

        // Answers with the request's own id, even for a request it cannot read, so the native side never waits on it.
        internal static async Task Handle(IEncorePlatform platform, string json)
        {
            var parsed = Wire.ParsePurchaseRequest(json);
            var reply = parsed == null
                ? Wire.PurchaseFailure("Unreadable purchase request: " + json)
                : await Reply(Controller, parsed.Value.Request);
            try
            {
                platform.AnswerPurchase(parsed?.Id ?? Wire.PurchaseRequestId(json), reply);
            }
            catch (Exception e)
            {
                Log.Contained("AnswerPurchase", e);
            }
        }

        // Never throws. A throw, a null task or an unknown result is a failure, never a phantom purchase.
        internal static async Task<string> Reply(IEncorePurchaseController controller, PurchaseRequest request)
        {
            if (controller == null) return Wire.PurchaseFailure("Encore asked for a purchase, but no IEncorePurchaseController is set.");
            try
            {
                var task = controller.Purchase(request);
                if (task == null) return Wire.PurchaseFailure("The purchase controller returned no task.");
                return Wire.PurchaseReply(await task);
            }
            catch (Exception e)
            {
                return Wire.PurchaseFailure(e.Message);
            }
        }
    }
}
