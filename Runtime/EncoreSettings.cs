using System;
using System.Threading;
using EncoreKit.Internal;

namespace EncoreKit
{
    // Runtime settings and the placement queries. A setting can be set before Configure; Configure sends it then.
    public static partial class Encore
    {
        private static bool _isClaimEnabled = true;
        private static PublisherRewardPolicy _publisherRewardPolicy = new PublisherRewardPolicy();

        // Whether the claim button on offer cards is tappable. Survives Reset. Android and iOS apply a change to an
        // open sheet; WebGL applies it from the next Show.
        public static bool IsClaimEnabled
        {
            get => _isClaimEnabled;
            set
            {
                _isClaimEnabled = value;
                if (_platform != null) Safely("IsClaimEnabled", () => _platform.SetClaimEnabled(value));
            }
        }

        // The daily limit Encore's server applies to a placement's PublisherReward. Set it any time before Show.
        // Survives Reset. Android and iOS; the web SDK has no policy, so WebGL keeps the server default.
        public static PublisherRewardPolicy PublisherRewardPolicy
        {
            get => _publisherRewardPolicy;
            set
            {
                _publisherRewardPolicy = value ?? throw new ArgumentNullException(nameof(value));
                if (_platform != null) Safely("PublisherRewardPolicy", () => _platform.SetPublisherRewardDailyLimit(value.DailyLimit));
            }
        }

        // Tests only: a fake platform in place of a configured one.
        internal static void SetPlatformForTests(IEncorePlatform platform) => _platform = platform;

        private static void ApplySettings(IEncorePlatform platform)
        {
            Safely("IsClaimEnabled", () => platform.SetClaimEnabled(_isClaimEnabled));
            Safely("PublisherRewardPolicy", () => platform.SetPublisherRewardDailyLimit(_publisherRewardPolicy.DailyLimit));
        }

        // A native failure in a setting is logged at Warn, never thrown into the game.
        private static void Safely(string name, Action call) => Log.Contain(name, call);

        internal static void Prefetch(string requestJson)
        {
            if (_platform != null) Safely("Prefetch", () => _platform.Prefetch(requestJson));
        }

        // The first answer wins, as for Show. No platform, or a request the bridges cannot read, answers false.
        internal static void HasOffers(string requestJson, Action<bool> onResult)
        {
            var platform = _platform;
            if (platform == null || requestJson == null)
            {
                MainThread.Post(() => onResult(false));
                return;
            }
            var answered = 0;
            try
            {
                platform.HasOffers(requestJson, json =>
                {
                    if (Interlocked.Exchange(ref answered, 1) != 0) return;
                    var hasOffers = Wire.ParseHasOffers(json);
                    MainThread.Post(() => onResult(hasOffers));
                });
            }
            catch (Exception e)
            {
                Log.Contained("HasOffers", e);
                if (Interlocked.Exchange(ref answered, 1) == 0) MainThread.Post(() => onResult(false));
            }
        }
    }
}
