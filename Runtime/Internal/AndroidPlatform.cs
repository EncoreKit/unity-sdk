#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace EncoreKit.Internal
{
    // Calls EncoreUnityBridge in Runtime/Plugins/Android/EncoreUnityBridge.aar. JNI cannot infer a signature from null, so absent strings go over as "".
    internal sealed partial class AndroidPlatform : IEncorePlatform
    {
        private const string BridgeClass = "com.encorekit.unity.EncoreUnityBridge";
        private const string CallbackInterface = "com.encorekit.unity.EncoreUnityCallback";

        private readonly AndroidJavaClass _bridge = new AndroidJavaClass(BridgeClass);
        private readonly AndroidJavaClass _giftsButton = new AndroidJavaClass("com.encorekit.unity.EncoreUnityGiftsButton");
        private readonly AndroidJavaClass _purchases = new AndroidJavaClass("com.encorekit.unity.EncoreUnityPurchases");

        // The Android SDK has no entitlement API, so these answer at once.
        private const string NotActive = "{\"active\":false}";
        private const string RevokeUnsupported = "{\"success\":false,\"errorType\":\"domain_error\",\"errorMessage\":\"RevokeEntitlements is not in the Android SDK.\"}";

        private sealed class Callback : AndroidJavaProxy
        {
            private readonly Action<string> _onMessage;

            public Callback(Action<string> onMessage) : base(CallbackInterface) => _onMessage = onMessage;

            // Called by name from Java, so code stripping must keep it. No catch here: Kotlin's deliver catches a throw
            // and needs to see it, because a purchase request C# did not take must fail at once.
            [UnityEngine.Scripting.Preserve]
            public void onMessage(string json) => _onMessage(json);
        }

        private static AndroidJavaObject CurrentActivity()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                return player.GetStatic<AndroidJavaObject>("currentActivity");
            }
        }

        public void Configure(string apiKey, string optionsJson, Action<string> onOutcomeJson, Action<string> onPurchaseJson) =>
            _bridge.CallStatic("configure", CurrentActivity(), apiKey, optionsJson, new Callback(onOutcomeJson), new Callback(onPurchaseJson));

        public void Identify(string userId, string attributesJson) =>
            _bridge.CallStatic("identify", userId, attributesJson ?? "");

        public void SetUserAttributes(string attributesJson) =>
            _bridge.CallStatic("setUserAttributes", attributesJson ?? "");

        public void SetLanguage(string tag) => _bridge.CallStatic("setLanguage", tag);

        public void ClearLanguage() => _bridge.CallStatic("clearLanguage");

        public void Reset() => _bridge.CallStatic("reset");

        public void ShowPlacement(string requestJson, Action<string> onResultJson) =>
            _bridge.CallStatic("showPlacement", CurrentActivity(), requestJson, new Callback(onResultJson));

        public void CreateGiftsButton(long id, string optionsJson, Action<string> onMessageJson) =>
            _giftsButton.CallStatic("create", CurrentActivity(), id, optionsJson, new Callback(onMessageJson));

        public void SetGiftsButtonFrame(long id, float x, float y, float width, float screenWidth, float screenHeight) =>
            _giftsButton.CallStatic("setFrame", id, x, y, width, screenWidth, screenHeight);

        public void SetGiftsButtonVisible(long id, bool visible) => _giftsButton.CallStatic("setVisible", id, visible);

        public void SetGiftsButtonVisibleFraction(long id, double fraction) => _giftsButton.CallStatic("setVisibleFraction", id, fraction);

        public void UpdateGiftsButton(long id, string updateJson) => _giftsButton.CallStatic("update", id, updateJson);

        public void DestroyGiftsButton(long id) => _giftsButton.CallStatic("destroy", id);

        public void AnswerPurchase(long requestId, string replyJson) => _purchases.CallStatic("answer", requestId, replyJson);

        public void IsActive(string queryJson, Action<string> onReplyJson) => onReplyJson(NotActive);

        public void SubscribeIsActive(long id, string queryJson, Action<string> onReplyJson) => onReplyJson(NotActive);

        public void UnsubscribeIsActive(long id) { }

        public void RevokeEntitlements(Action<string> onReplyJson) => onReplyJson(RevokeUnsupported);

        public void LinkPurchase(string purchaseToken) => _purchases.CallStatic("linkPurchase", purchaseToken);

        public string CurrentEntitlement() => _purchases.CallStatic<string>("currentEntitlement");

        public void ClearOfferCache() => _purchases.CallStatic("clearOfferCache");
    }
}
#endif
