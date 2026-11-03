using System;
using System.Threading.Tasks;
using EncoreKit.Internal;

namespace EncoreKit
{
    // Prefetch, HasOffers and the loading state: the placement calls beside Show.
    public sealed partial class Placement
    {
        // True when Show starts and false when its result arrives, before the result callback, as the Swift and web
        // builders do. Raised on the main thread on every platform.
        public event Action<bool> OnLoadingStateChange;

        // Warms this placement's offers so Show opens without a network round trip. Call it when the surface comes into
        // view. iOS only: the Android SDK 2.5.0 and the web SDK have no placement prefetch, so there it does nothing.
        public void Prefetch()
        {
            var request = RequestOrNull();
            if (request != null) Encore.Prefetch(request);
        }

        // Whether Show would find offers for this user now: one answer, on the main thread. Select UseCase.RewardUsers
        // first: UseCase.ReduceChurn, the default, answers false, as the native SDKs do. Also false before Configure, in
        // the Editor and on any failure. Android, iOS and WebGL.
        public void HasOffers(Action<bool> onResult)
        {
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));
            Encore.HasOffers(RequestOrNull(), onResult);
        }

        public Task<bool> HasOffersAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            HasOffers(hasOffers => tcs.TrySetResult(hasOffers));
            return tcs.Task;
        }

        // Null for a use case the bridges do not know.
        private string RequestOrNull()
        {
            try
            {
                return Wire.PlacementRequest(_placementId, _useCase, null, null);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        // A throw in the game's handler must not stop the result reaching the caller.
        private Action<PresentationResult> WithLoadingState(Action<PresentationResult> onResult)
        {
            var handler = OnLoadingStateChange;
            if (handler == null) return onResult;
            MainThread.Post(() => MainThread.Raise(handler, true));
            return result =>
            {
                MainThread.Raise(handler, false);
                onResult(result);
            };
        }
    }
}
