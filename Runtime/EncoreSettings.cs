using System;
using System.Threading;
using EncoreKit.Internal;

namespace EncoreKit
{
    // Runtime settings and the placement queries. A setting can be set before Configure; Configure sends it then.
    public static partial class Encore
    {
        private static bool _isClaimEnabled = true;
        private static bool _isEnabled = true;
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

        // Turns the whole SDK off (false) or back on (true), for a game whose player turns off an in-app deals setting.
        // Default true; set it any time, before or after Configure. While off the native SDK sends nothing (no analytics or
        // app open, foreground and background events, no error reports, no background network), Show answers
        // NotPresentedReason.Disabled with no UI, and triggers show nothing. A sheet already open finishes. Nothing dropped
        // while off is sent later; turning it back on resumes the SDK's own work. Not persisted: every launch starts on, so
        // set it from the game's own setting at launch. Configure sends it before the native configure, so a game set off
        // never starts the SDK. After Configure, a value the SDK refused is not kept: IsEnabled stays as the SDK is. Android,
        // iOS and WebGL (a bundled web SDK older than 2.7.0 has no switch, so off is refused there).
        public static bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                // After Configure, the value the native SDK took: one it refused leaves IsEnabled as the SDK still is.
                if (_platform == null || Log.Contain("IsEnabled", () => _platform.SetEnabled(value))) _isEnabled = value;
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

        // Before the native configure, so the SDK never starts while the game has it off. False when the game has it off
        // and the native SDK did not take it: Configure then does not start the SDK (fail closed).
        private static bool ApplySettingsBeforeConfigure(IEncorePlatform platform) =>
            Log.Contain("IsEnabled", () => platform.SetEnabled(_isEnabled), () => platform) || _isEnabled;

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
