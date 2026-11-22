#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;

namespace EncoreKit.Internal
{
    // Calls Runtime/Plugins/WebGL/EncoreUnity.jslib, which drives the bundled @encorekit/web-sdk. Ids cross as int,
    // because a wasm32 callback signature cannot carry a long safely. Everything runs on the browser's one thread.
    internal sealed partial class WebGLPlatform : IEncorePlatform
    {
        private delegate void JsCallback(int requestId, IntPtr json);

        [DllImport("__Internal")] private static extern void EncoreUnity_Configure(string apiKey, string optionsJson, JsCallback onOutcome);
        [DllImport("__Internal")] private static extern void EncoreUnity_Identify(string userId, string attributesJson);
        [DllImport("__Internal")] private static extern void EncoreUnity_SetUserAttributes(string attributesJson);
        [DllImport("__Internal")] private static extern void EncoreUnity_SetLanguage(string tag);
        [DllImport("__Internal")] private static extern void EncoreUnity_ClearLanguage();
        [DllImport("__Internal")] private static extern void EncoreUnity_Reset();
        [DllImport("__Internal")] private static extern void EncoreUnity_ShowPlacement(int requestId, string requestJson, JsCallback onResult);
        [DllImport("__Internal")] private static extern void EncoreUnity_GiftsButtonCreate(int id, string optionsJson, JsCallback onMessage);
        [DllImport("__Internal")] private static extern void EncoreUnity_GiftsButtonSetFrame(int id, float x, float y, float width, float screenWidth, float screenHeight);
        [DllImport("__Internal")] private static extern void EncoreUnity_GiftsButtonSetVisible(int id, int visible);
        [DllImport("__Internal")] private static extern void EncoreUnity_GiftsButtonUpdate(int id, string updateJson);
        [DllImport("__Internal")] private static extern void EncoreUnity_GiftsButtonDestroy(int id);
        [DllImport("__Internal")] private static extern void EncoreUnity_RevokeEntitlements(int requestId, JsCallback onReply);

        // IL2CPP needs static callbacks; holding the delegates keeps them alive for JavaScript.
        private static readonly JsCallback ResultCallback = OnResult;
        private static readonly JsCallback OutcomeCallback = OnOutcome;
        private static readonly JsCallback GiftsButtonCallback = OnGiftsButtonMessage;
        private static readonly Dictionary<int, Action<string>> Pending = new Dictionary<int, Action<string>>();
        private static readonly Dictionary<int, Action<string>> GiftsButtons = new Dictionary<int, Action<string>>();
        private static Action<string> _onOutcome;
        private static int _nextRequestId;

        // The web SDK has no purchase controller and never asks for a purchase, so onPurchaseJson is not kept.
        public void Configure(string apiKey, string optionsJson, Action<string> onOutcomeJson, Action<string> onPurchaseJson)
        {
            _onOutcome = onOutcomeJson;
            EncoreUnity_Configure(apiKey, optionsJson, OutcomeCallback);
        }

        public void Identify(string userId, string attributesJson) => EncoreUnity_Identify(userId, attributesJson);
        public void SetUserAttributes(string attributesJson) => EncoreUnity_SetUserAttributes(attributesJson);
        public void SetLanguage(string tag) => EncoreUnity_SetLanguage(tag);
        public void ClearLanguage() => EncoreUnity_ClearLanguage();
        public void Reset() => EncoreUnity_Reset();

        public void ShowPlacement(string requestJson, Action<string> onResultJson)
        {
            var id = ++_nextRequestId;
            Pending[id] = onResultJson;
            EncoreUnity_ShowPlacement(id, requestJson, ResultCallback);
        }

        public void CreateGiftsButton(long id, string optionsJson, Action<string> onMessageJson)
        {
            GiftsButtons[(int)id] = onMessageJson;
            EncoreUnity_GiftsButtonCreate((int)id, optionsJson, GiftsButtonCallback);
        }

        public void SetGiftsButtonFrame(long id, float x, float y, float width, float screenWidth, float screenHeight) =>
            EncoreUnity_GiftsButtonSetFrame((int)id, x, y, width, screenWidth, screenHeight);

        public void SetGiftsButtonVisible(long id, bool visible) => EncoreUnity_GiftsButtonSetVisible((int)id, visible ? 1 : 0);

        // The web SDK measures what the page shows itself and has no share to set, so only shown or hidden crosses.
        public void SetGiftsButtonVisibleFraction(long id, double fraction) => EncoreUnity_GiftsButtonSetVisible((int)id, fraction <= 0 ? 0 : 1);

        public void UpdateGiftsButton(long id, string updateJson) => EncoreUnity_GiftsButtonUpdate((int)id, updateJson);

        public void DestroyGiftsButton(long id)
        {
            GiftsButtons.Remove((int)id);
            EncoreUnity_GiftsButtonDestroy((int)id);
        }

        [MonoPInvokeCallback(typeof(JsCallback))]
        private static void OnResult(int requestId, IntPtr json)
        {
            if (!Pending.TryGetValue(requestId, out var callback)) return;
            Pending.Remove(requestId);
            callback(Marshal.PtrToStringUTF8(json));
        }

        [MonoPInvokeCallback(typeof(JsCallback))]
        private static void OnGiftsButtonMessage(int id, IntPtr json)
        {
            if (GiftsButtons.TryGetValue(id, out var handler)) handler(Marshal.PtrToStringUTF8(json));
        }

        [MonoPInvokeCallback(typeof(JsCallback))]
        private static void OnOutcome(int requestId, IntPtr json) => _onOutcome?.Invoke(Marshal.PtrToStringUTF8(json));

        public void AnswerPurchase(long requestId, string replyJson) { }

        // The web SDK checks entitlements on the server only; its isActive() always answers false.
        private const string NotActive = "{\"active\":false}";
        public void IsActive(string queryJson, Action<string> onReplyJson) => onReplyJson(NotActive);
        public void SubscribeIsActive(long id, string queryJson, Action<string> onReplyJson) => onReplyJson(NotActive);
        public void UnsubscribeIsActive(long id) { }

        public void RevokeEntitlements(Action<string> onReplyJson)
        {
            var id = ++_nextRequestId;
            Pending[id] = onReplyJson;
            EncoreUnity_RevokeEntitlements(id, ResultCallback);
        }

        // Android only in the native SDKs.
        public void LinkPurchase(string purchaseToken) { }
        public string CurrentEntitlement() => null;
        public void ClearOfferCache() { }
    }
}
#endif
