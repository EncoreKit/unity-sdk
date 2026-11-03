using System;

namespace EncoreKit.Internal
{
    // One per runtime. Callbacks may arrive on any thread; Encore moves them to the Unity main thread.
    internal partial interface IEncorePlatform
    {
        // optionsJson is Wire.ConfigureOptions. onPurchaseJson gets each purchase request; answer it with AnswerPurchase.
        void Configure(string apiKey, string optionsJson, Action<string> onOutcomeJson, Action<string> onPurchaseJson);
        void Identify(string userId, string attributesJson);
        void SetUserAttributes(string attributesJson);
        void SetLanguage(string tag);
        void ClearLanguage();
        void Reset();
        void ShowPlacement(string requestJson, Action<string> onResultJson);

        // Frames are Unity screen pixels with a top-left origin; the native side scales to its own units.
        void CreateGiftsButton(long id, string optionsJson, Action<string> onMessageJson);
        void SetGiftsButtonFrame(long id, float x, float y, float width, float screenWidth, float screenHeight);
        void SetGiftsButtonVisible(long id, bool visible);
        void UpdateGiftsButton(long id, string updateJson);
        void DestroyGiftsButton(long id);

        // Purchases and entitlements. Each reply is one JSON message; see Wire.Purchases.cs.
        void AnswerPurchase(long requestId, string replyJson);
        void IsActive(string queryJson, Action<string> onReplyJson);
        void SubscribeIsActive(long id, string queryJson, Action<string> onReplyJson);
        void UnsubscribeIsActive(long id);
        void RevokeEntitlements(Action<string> onReplyJson);
        void LinkPurchase(string purchaseToken);
        // JSON, or null or "" when no IAP product is configured.
        string CurrentEntitlement();
        void ClearOfferCache();
    }

    internal static class Platforms
    {
        // Tests only: a fake platform for Encore.Configure.
        internal static Func<IEncorePlatform> CreateForTests;

        internal static IEncorePlatform Create()
        {
            if (CreateForTests != null) return CreateForTests();
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidPlatform();
#elif UNITY_IOS && !UNITY_EDITOR
            return new IosPlatform();
#elif UNITY_WEBGL && !UNITY_EDITOR
            return new WebGLPlatform();
#else
            return new EditorPlatform();
#endif
        }
    }
}
