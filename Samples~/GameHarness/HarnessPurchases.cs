using System;
using System.Threading.Tasks;

namespace EncoreKit.GameHarness
{
    // A store-free purchase controller: it logs the request Encore sends and answers Purchased after a short wait.
    public sealed class HarnessPurchases : IEncorePurchaseController
    {
        private readonly Action<string> _log;

        public HarnessPurchases(Action<string> log) => _log = log;

        public async Task<PurchaseResult> Purchase(PurchaseRequest request)
        {
            _log($"purchase request: product {request.ProductId}, placement {request.PlacementId ?? "none"}, " +
                 $"promo {request.PromoOfferId ?? "none"}, base plan {request.BasePlanId ?? "none"}");
            await Task.Delay(500);
            _log("purchase answered: Purchased");
            return PurchaseResult.Purchased;
        }
    }
}
