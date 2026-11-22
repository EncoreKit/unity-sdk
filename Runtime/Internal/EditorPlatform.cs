using System;

namespace EncoreKit.Internal
{
    // The native SDKs do not run in the Editor or on other targets, so every placement reports not presented.
    internal sealed partial class EditorPlatform : IEncorePlatform
    {
        private const string Result = "{\"type\":\"notPresented\",\"reason\":\"error\",\"errorType\":\"domain_error\",\"errorMessage\":\"Encore presents on Android, iOS and WebGL builds only.\"}";

        public void Configure(string apiKey, string optionsJson, Action<string> onOutcomeJson, Action<string> onPurchaseJson) =>
            Log.Info("Configured in the Editor stub. Placements show on Android, iOS and WebGL builds only.");

        public void Identify(string userId, string attributesJson) { }
        public void SetUserAttributes(string attributesJson) { }
        public void SetLanguage(string tag) { }
        public void ClearLanguage() { }
        public void Reset() { }

        public void ShowPlacement(string requestJson, Action<string> onResultJson) => onResultJson(Result);

        // Nothing to show, so the button reports zero height, as a native one with no offers does.
        public void CreateGiftsButton(long id, string optionsJson, Action<string> onMessageJson) =>
            onMessageJson("{\"event\":\"height\",\"height\":0}");

        public void SetGiftsButtonFrame(long id, float x, float y, float width, float screenWidth, float screenHeight) { }
        public void SetGiftsButtonVisible(long id, bool visible) { }
        public void SetGiftsButtonVisibleFraction(long id, double fraction) { }
        public void UpdateGiftsButton(long id, string updateJson) { }
        public void DestroyGiftsButton(long id) { }

        // No native SDK, so nothing asks for a purchase and no entitlement is active.
        public void AnswerPurchase(long requestId, string replyJson) { }
        public void IsActive(string queryJson, Action<string> onReplyJson) => onReplyJson("{\"active\":false}");
        public void SubscribeIsActive(long id, string queryJson, Action<string> onReplyJson) => onReplyJson("{\"active\":false}");
        public void UnsubscribeIsActive(long id) { }
        public void RevokeEntitlements(Action<string> onReplyJson) =>
            onReplyJson("{\"success\":false,\"errorType\":\"domain_error\",\"errorMessage\":\"Encore runs on Android, iOS and WebGL builds only.\"}");
        public void LinkPurchase(string purchaseToken) { }
        public string CurrentEntitlement() => null;
        public void ClearOfferCache() { }
    }
}
