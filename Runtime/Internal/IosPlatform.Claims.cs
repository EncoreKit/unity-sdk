#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;

namespace EncoreKit.Internal
{
    // Calls the @_cdecl functions in Runtime/Plugins/iOS/EncoreUnityClaim.swift.
    internal sealed partial class IosPlatform
    {
        [DllImport("__Internal")] private static extern void EncoreUnity_ClaimCreate(long id, string optionsJson, NativeCallback onMessage);
        [DllImport("__Internal")] private static extern void EncoreUnity_ClaimDestroy(long id);
        [DllImport("__Internal")] private static extern void EncoreUnity_ClaimSetVisible(long id, double fraction);
        [DllImport("__Internal")] private static extern void EncoreUnity_ClaimTap(long id, string target, long requestId, NativeCallback onResult);
        [DllImport("__Internal")] private static extern void EncoreUnity_ClaimDismiss(long id);
        [DllImport("__Internal")] private static extern void EncoreUnity_ClaimSetPublisherReward(long id, string json);
        [DllImport("__Internal")] private static extern void EncoreUnity_ClaimSetSheetText(long id, string json);

        // Claims live until destroyed, so their handlers stay registered by id, as the gift buttons' do.
        private static readonly NativeCallback ClaimCallback = OnClaimMessage;
        private static readonly Dictionary<long, Action<string>> Claims = new Dictionary<long, Action<string>>();

        public void ClaimCreate(long id, string optionsJson, Action<string> onMessageJson)
        {
            lock (Claims) Claims[id] = onMessageJson;
            EncoreUnity_ClaimCreate(id, optionsJson, ClaimCallback);
        }

        public void ClaimDestroy(long id)
        {
            lock (Claims) Claims.Remove(id);
            EncoreUnity_ClaimDestroy(id);
        }

        public void ClaimSetVisible(long id, double fraction) => EncoreUnity_ClaimSetVisible(id, fraction);

        // The reply goes through Pending, so a tap in flight at destroy is still answered.
        public void ClaimTap(long id, string target, Action<string> onResultJson) =>
            EncoreUnity_ClaimTap(id, target ?? "", Register(onResultJson), ResultCallback);

        public void ClaimDismiss(long id) => EncoreUnity_ClaimDismiss(id);

        public void ClaimSetPublisherReward(long id, string json) => EncoreUnity_ClaimSetPublisherReward(id, json);

        public void ClaimSetSheetText(long id, string json) => EncoreUnity_ClaimSetSheetText(id, json);

        [MonoPInvokeCallback(typeof(NativeCallback))]
        private static void OnClaimMessage(long id, IntPtr json)
        {
            Action<string> handler;
            lock (Claims)
            {
                if (!Claims.TryGetValue(id, out handler)) return;
            }
            Deliver(handler, json);
        }
    }
}
#endif
