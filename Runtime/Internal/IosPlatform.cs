#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;

namespace EncoreKit.Internal
{
    // Calls the @_cdecl functions in Runtime/Plugins/iOS/EncoreUnityBridge.swift. Strings cross as UTF-8.
    internal sealed partial class IosPlatform : IEncorePlatform
    {
        private delegate void NativeCallback(long requestId, IntPtr json);

        [DllImport("__Internal")] private static extern void EncoreUnity_Configure(string apiKey, string optionsJson, NativeCallback onOutcome, NativeCallback onPurchase);
        [DllImport("__Internal")] private static extern void EncoreUnity_Identify(string userId, string attributesJson);
        [DllImport("__Internal")] private static extern void EncoreUnity_SetUserAttributes(string attributesJson);
        [DllImport("__Internal")] private static extern void EncoreUnity_SetLanguage(string tag);
        [DllImport("__Internal")] private static extern void EncoreUnity_ClearLanguage();
        [DllImport("__Internal")] private static extern void EncoreUnity_Reset();
        [DllImport("__Internal")] private static extern void EncoreUnity_ShowPlacement(long requestId, string requestJson, NativeCallback onResult);
        [DllImport("__Internal")] private static extern void EncoreUnity_GiftsButtonCreate(long id, string optionsJson, NativeCallback onMessage);
        [DllImport("__Internal")] private static extern void EncoreUnity_GiftsButtonSetFrame(long id, float x, float y, float width, float screenWidth, float screenHeight);
        [DllImport("__Internal")] private static extern void EncoreUnity_GiftsButtonSetVisible(long id, [MarshalAs(UnmanagedType.I1)] bool visible);
        [DllImport("__Internal")] private static extern void EncoreUnity_GiftsButtonUpdate(long id, string updateJson);
        [DllImport("__Internal")] private static extern void EncoreUnity_GiftsButtonDestroy(long id);
        [DllImport("__Internal")] private static extern void EncoreUnity_AnswerPurchase(long requestId, string replyJson);
        [DllImport("__Internal")] private static extern void EncoreUnity_IsActive(long requestId, string queryJson, NativeCallback onReply);
        [DllImport("__Internal")] private static extern void EncoreUnity_IsActiveSubscribe(long id, string queryJson, NativeCallback onReply);
        [DllImport("__Internal")] private static extern void EncoreUnity_IsActiveUnsubscribe(long id);
        [DllImport("__Internal")] private static extern void EncoreUnity_RevokeEntitlements(long requestId, NativeCallback onReply);

        // IL2CPP needs static callbacks; holding the delegates keeps them alive for native code.
        private static readonly NativeCallback ResultCallback = OnResult;
        private static readonly NativeCallback OutcomeCallback = OnOutcome;
        private static readonly Dictionary<long, Action<string>> Pending = new Dictionary<long, Action<string>>();
        private static readonly NativeCallback PurchaseCallback = OnPurchase;
        private static Action<string> _onOutcome;
        private static Action<string> _onPurchase;
        private static long _nextRequestId;

        public void Configure(string apiKey, string optionsJson, Action<string> onOutcomeJson, Action<string> onPurchaseJson)
        {
            _onOutcome = onOutcomeJson;
            _onPurchase = onPurchaseJson;
            EncoreUnity_Configure(apiKey, optionsJson, OutcomeCallback, PurchaseCallback);
        }

        public void Identify(string userId, string attributesJson) => EncoreUnity_Identify(userId, attributesJson);
        public void SetUserAttributes(string attributesJson) => EncoreUnity_SetUserAttributes(attributesJson);
        public void SetLanguage(string tag) => EncoreUnity_SetLanguage(tag);
        public void ClearLanguage() => EncoreUnity_ClearLanguage();
        public void Reset() => EncoreUnity_Reset();

        public void ShowPlacement(string requestJson, Action<string> onResultJson) =>
            EncoreUnity_ShowPlacement(Register(onResultJson), requestJson, ResultCallback);

        private static long Register(Action<string> onReply)
        {
            lock (Pending)
            {
                var id = ++_nextRequestId;
                Pending[id] = onReply;
                return id;
            }
        }

        [MonoPInvokeCallback(typeof(NativeCallback))]
        private static void OnResult(long requestId, IntPtr json)
        {
            Action<string> callback;
            lock (Pending)
            {
                if (!Pending.TryGetValue(requestId, out callback)) return;
                Pending.Remove(requestId);
            }
            Deliver(callback, json);
        }

        // A throw must never unwind into the Swift frame that called back, because that crashes the game.
        private static void Deliver(Action<string> handler, IntPtr json)
        {
            try
            {
                handler?.Invoke(Marshal.PtrToStringUTF8(json));
            }
            catch (Exception e)
            {
                Log.Contained("iOS callback", e);
            }
        }

        // Gift buttons live until destroyed, so their handlers stay registered by id rather than in Pending.
        private static readonly NativeCallback GiftsButtonCallback = OnGiftsButtonMessage;
        private static readonly Dictionary<long, Action<string>> GiftsButtons = new Dictionary<long, Action<string>>();

        public void CreateGiftsButton(long id, string optionsJson, Action<string> onMessageJson)
        {
            lock (GiftsButtons) GiftsButtons[id] = onMessageJson;
            EncoreUnity_GiftsButtonCreate(id, optionsJson, GiftsButtonCallback);
        }

        public void SetGiftsButtonFrame(long id, float x, float y, float width, float screenWidth, float screenHeight) =>
            EncoreUnity_GiftsButtonSetFrame(id, x, y, width, screenWidth, screenHeight);

        public void SetGiftsButtonVisible(long id, bool visible) => EncoreUnity_GiftsButtonSetVisible(id, visible);

        public void UpdateGiftsButton(long id, string updateJson) => EncoreUnity_GiftsButtonUpdate(id, updateJson);

        public void DestroyGiftsButton(long id)
        {
            lock (GiftsButtons) GiftsButtons.Remove(id);
            EncoreUnity_GiftsButtonDestroy(id);
        }

        [MonoPInvokeCallback(typeof(NativeCallback))]
        private static void OnGiftsButtonMessage(long id, IntPtr json)
        {
            Action<string> handler;
            lock (GiftsButtons)
            {
                if (!GiftsButtons.TryGetValue(id, out handler)) return;
            }
            Deliver(handler, json);
        }

        [MonoPInvokeCallback(typeof(NativeCallback))]
        private static void OnOutcome(long requestId, IntPtr json) => Deliver(_onOutcome, json);

        // The purchase request carries its own requestId in the JSON.
        [MonoPInvokeCallback(typeof(NativeCallback))]
        private static void OnPurchase(long requestId, IntPtr json) => Deliver(_onPurchase, json);

        public void AnswerPurchase(long requestId, string replyJson) => EncoreUnity_AnswerPurchase(requestId, replyJson);

        public void IsActive(string queryJson, Action<string> onReplyJson) =>
            EncoreUnity_IsActive(Register(onReplyJson), queryJson, ResultCallback);

        // Subscriptions live until unsubscribed, so they share the gift buttons' by-id dispatch, not Pending.
        private static readonly NativeCallback SubscriptionCallback = OnSubscription;
        private static readonly Dictionary<long, Action<string>> Subscriptions = new Dictionary<long, Action<string>>();

        public void SubscribeIsActive(long id, string queryJson, Action<string> onReplyJson)
        {
            lock (Subscriptions) Subscriptions[id] = onReplyJson;
            EncoreUnity_IsActiveSubscribe(id, queryJson, SubscriptionCallback);
        }

        public void UnsubscribeIsActive(long id)
        {
            lock (Subscriptions) Subscriptions.Remove(id);
            EncoreUnity_IsActiveUnsubscribe(id);
        }

        [MonoPInvokeCallback(typeof(NativeCallback))]
        private static void OnSubscription(long id, IntPtr json)
        {
            Action<string> handler;
            lock (Subscriptions)
            {
                if (!Subscriptions.TryGetValue(id, out handler)) return;
            }
            Deliver(handler, json);
        }

        public void RevokeEntitlements(Action<string> onReplyJson) => EncoreUnity_RevokeEntitlements(Register(onReplyJson), ResultCallback);

        // Android only in the native SDKs.
        public void LinkPurchase(string purchaseToken) { }
        public string CurrentEntitlement() => null;
        public void ClearOfferCache() { }
    }
}
#endif
