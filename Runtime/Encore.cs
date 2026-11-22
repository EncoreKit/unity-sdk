using System;
using System.Threading;
using EncoreKit.Internal;

namespace EncoreKit
{
    // Entry point. Call from the Unity main thread; every callback and event also arrives on it.
    public static partial class Encore
    {
        private static IEncorePlatform _platform;

        // Raised for every placement result, presented or not, wherever the placement was shown from.
        public static event Action<PlacementOutcome> Outcomes;

        public static bool IsConfigured => _platform != null;

        internal static IEncorePlatform Platform => _platform;

        public static void Configure(string apiKey, LogLevel logLevel = LogLevel.None) =>
            Configure(apiKey, null, new EncoreOptions { LogLevel = logLevel });

        public static void Configure(string apiKey, EncoreOptions options) => Configure(apiKey, null, options);

        // With no purchase controller the SDK never attempts a purchase. Configure once: a second call is ignored,
        // as the native SDKs ignore it, so the controller and options cannot change later.
        public static void Configure(string apiKey, IEncorePurchaseController purchaseController, EncoreOptions options = null)
        {
            if (string.IsNullOrEmpty(apiKey)) throw new ArgumentException("apiKey is required", nameof(apiKey));
            if (_platform != null)
            {
                Log.Warn("Already configured. Ignoring this Configure call.");
                return;
            }
            options ??= new EncoreOptions();
            Log.Level = options.LogLevel;
            MainThread.CaptureCurrent();
            IEncorePlatform platform = null;
            PurchaseBridge.Controller = purchaseController;
            var optionsJson = Wire.ConfigureOptions(options, purchaseController != null);
            // A synchronous throw (a missing bridge class or entry point) leaves Encore unconfigured, so Configure can run again.
            // So does an off switch the native SDK did not take: the game has the SDK off, so it does not start.
            // _platform is still null here, so failures report to the local platform.
            var offNotTaken = false;
            if (!Log.Contain("Configure", () =>
                {
                    platform = Platforms.Create();
                    // Before Configure, so the SDK's first events carry it. A bridge without it still configures.
                    var created = platform;
                    Log.Contain("SetWrapper", () => created.SetWrapper(PackageInfo.Name, PackageInfo.Version), () => created);
                    if (!ApplySettingsBeforeConfigure(created))
                    {
                        offNotTaken = true;
                        return;
                    }
                    platform.Configure(apiKey, optionsJson, OnOutcomeJson, json => PurchaseBridge.OnRequest(platform, json));
                }, () => platform) || offNotTaken)
            {
                PurchaseBridge.Controller = null;
                return;
            }
            // Set first, so a failure while the settings are sent is reported: the native configure already ran.
            _platform = platform;
            ApplySettings(platform);
        }

        public static void Identify(string userId, UserAttributes attributes = null)
        {
            if (string.IsNullOrEmpty(userId)) throw new ArgumentException("userId is required", nameof(userId));
            Call("Identify", p => p.Identify(userId, Wire.Attributes(attributes)));
        }

        public static void SetUserAttributes(UserAttributes attributes)
        {
            if (attributes == null) throw new ArgumentNullException(nameof(attributes));
            var json = Wire.Attributes(attributes);
            if (json != null) Call("SetUserAttributes", p => p.SetUserAttributes(json));
        }

        // A BCP 47 tag such as "es" or "pt-BR". Overrides the device language for Encore copy.
        public static void SetLanguage(string tag)
        {
            if (string.IsNullOrEmpty(tag)) throw new ArgumentException("tag is required", nameof(tag));
            Call("SetLanguage", p => p.SetLanguage(tag));
        }

        public static void ClearLanguage() => Call("ClearLanguage", p => p.ClearLanguage());

        // Call on logout. Clears the identified user.
        public static void Reset() => Call("Reset", p => p.Reset());

        public static Placement Placement(string placementId) => new Placement(placementId);

        internal static void Show(string requestJson, Action<PresentationResult> onResult)
        {
            if (_platform == null)
            {
                MainThread.Post(() => onResult(PresentationResult.NotPresentedBecause(NotPresentedReason.NotConfigured)));
                return;
            }
            // The first answer wins: a bridge that answers twice, or answers and then throws, reaches the game once.
            var answered = 0;
            try
            {
                _platform.ShowPlacement(requestJson, json =>
                {
                    if (Interlocked.Exchange(ref answered, 1) != 0) return;
                    var result = Wire.ParseResult(json);
                    MainThread.Post(() => onResult(result));
                });
            }
            catch (Exception e)
            {
                // For example no current Activity on Android. Report it as a result, as the native SDKs do.
                Log.Contained("ShowPlacement", e);
                if (Interlocked.Exchange(ref answered, 1) != 0) return;
                MainThread.Post(() => onResult(PresentationResult.NotPresentedBecause(NotPresentedReason.Error, e.Message)));
            }
        }

        private static void OnOutcomeJson(string json)
        {
            var outcome = Wire.ParseOutcome(json);
            if (outcome == null) return;
            MainThread.Post(() => MainThread.Raise(Outcomes, outcome));
        }

        // A platform call with no result: a no-op before Configure, and a native throw never reaches the game.
        private static void Call(string name, Action<IEncorePlatform> call)
        {
            var platform = _platform;
            if (platform != null) Log.Contain(name, () => call(platform));
        }
    }
}
