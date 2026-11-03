using System;
using System.Threading;
using System.Threading.Tasks;
using EncoreKit.Internal;

namespace EncoreKit
{
    // Entitlements and the Android-only purchase calls. Every callback arrives on the Unity main thread.
    public static partial class Encore
    {
        private static long _nextSubscriptionId;

        // iOS only. Android answers false (the Android SDK has no entitlement API), and so does WebGL (the web SDK
        // checks entitlements on the server only).
        public static void IsActive(Entitlement entitlement, EntitlementScope scope, Action<bool> onResult)
        {
            if (entitlement == null) throw new ArgumentNullException(nameof(entitlement));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));
            if (_platform == null)
            {
                MainThread.Post(() => onResult(false));
                return;
            }
            // The first answer wins, as for Show.
            var answered = 0;
            try
            {
                _platform.IsActive(Wire.EntitlementQuery(entitlement, scope), json =>
                {
                    if (Interlocked.Exchange(ref answered, 1) != 0) return;
                    var active = Wire.ParseActive(json);
                    MainThread.Post(() => onResult(active));
                });
            }
            catch (Exception e)
            {
                Log.Contained("IsActive", e);
                if (Interlocked.Exchange(ref answered, 1) == 0) MainThread.Post(() => onResult(false));
            }
        }

        public static void IsActive(Entitlement entitlement, Action<bool> onResult) => IsActive(entitlement, EntitlementScope.All, onResult);

        public static Task<bool> IsActiveAsync(Entitlement entitlement, EntitlementScope scope = EntitlementScope.All)
        {
            var tcs = new TaskCompletionSource<bool>();
            IsActive(entitlement, scope, active => tcs.TrySetResult(active));
            return tcs.Task;
        }

        // Calls onChange with the current state, then on each change, until the handle is disposed. iOS only;
        // elsewhere it calls onChange(false) once, as IsActive answers.
        public static IDisposable IsActivePublisher(Entitlement entitlement, EntitlementScope scope, Action<bool> onChange)
        {
            if (entitlement == null) throw new ArgumentNullException(nameof(entitlement));
            if (onChange == null) throw new ArgumentNullException(nameof(onChange));
            var platform = _platform;
            if (platform == null)
            {
                MainThread.Post(() => onChange(false));
                return new Subscription(null);
            }
            string query;
            try
            {
                query = Wire.EntitlementQuery(entitlement, scope);
            }
            catch (ArgumentOutOfRangeException)
            {
                MainThread.Post(() => onChange(false));
                return new Subscription(null);
            }
            var id = Interlocked.Increment(ref _nextSubscriptionId);
            var subscription = new Subscription(() => Log.Contain("UnsubscribeIsActive", () => platform.UnsubscribeIsActive(id)));
            var answered = 0;
            var subscribed = Log.Contain("SubscribeIsActive", () => platform.SubscribeIsActive(id, query, json =>
            {
                Interlocked.Exchange(ref answered, 1);
                var active = Wire.ParseActive(json);
                MainThread.Post(() =>
                {
                    if (!subscription.IsDisposed) onChange(active);
                });
            }));
            // A subscribe that throws before any answer reads as not active; one that answered first keeps that answer.
            if (!subscribed && Volatile.Read(ref answered) == 0)
            {
                MainThread.Post(() =>
                {
                    if (!subscription.IsDisposed) onChange(false);
                });
            }
            return subscription;
        }

        public static IDisposable IsActivePublisher(Entitlement entitlement, Action<bool> onChange) =>
            IsActivePublisher(entitlement, EntitlementScope.All, onChange);

        // Admin and debug only. Android does not have it and answers a failure.
        public static void RevokeEntitlements(Action<EncoreCallResult> onComplete)
        {
            if (onComplete == null) throw new ArgumentNullException(nameof(onComplete));
            if (_platform == null)
            {
                MainThread.Post(() => onComplete(Wire.Failure("Encore is not configured.", "not_configured")));
                return;
            }
            var answered = 0;
            try
            {
                _platform.RevokeEntitlements(json =>
                {
                    if (Interlocked.Exchange(ref answered, 1) != 0) return;
                    var result = Wire.ParseCallResult(json);
                    MainThread.Post(() => onComplete(result));
                });
            }
            catch (Exception e)
            {
                Log.Contained("RevokeEntitlements", e);
                if (Interlocked.Exchange(ref answered, 1) == 0) MainThread.Post(() => onComplete(Wire.Failure(e.Message)));
            }
        }

        public static Task<EncoreCallResult> RevokeEntitlementsAsync()
        {
            var tcs = new TaskCompletionSource<EncoreCallResult>();
            RevokeEntitlements(result => tcs.TrySetResult(result));
            return tcs.Task;
        }

        // Android only: links a Play purchase made outside Encore's flow to the user, at once. Elsewhere a no-op.
        public static void LinkPurchase(string purchaseToken)
        {
            if (string.IsNullOrEmpty(purchaseToken)) throw new ArgumentException("purchaseToken is required", nameof(purchaseToken));
            Call("LinkPurchase", p => p.LinkPurchase(purchaseToken));
        }

        // Android only: the IAP product Encore offers now. Null elsewhere, before configure, or with none set.
        public static IapEntitlement CurrentEntitlement()
        {
            try
            {
                return Wire.ParseIapEntitlement(_platform?.CurrentEntitlement());
            }
            catch (Exception e)
            {
                Log.Contained("CurrentEntitlement", e);
                return null;
            }
        }

        // Android only: drops prefetched offers and images, for testing. Elsewhere a no-op.
        public static void ClearOfferCache() => Call("ClearOfferCache", p => p.ClearOfferCache());

        private sealed class Subscription : IDisposable
        {
            private Action _cancel;

            public Subscription(Action cancel) => _cancel = cancel;

            public bool IsDisposed => _cancel == null;

            public void Dispose()
            {
                var cancel = Interlocked.Exchange(ref _cancel, null);
                cancel?.Invoke();
            }
        }
    }
}
