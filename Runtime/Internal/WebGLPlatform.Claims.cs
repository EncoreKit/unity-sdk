#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;

namespace EncoreKit.Internal
{
    // Calls the EncoreUnity_Claim* functions in Runtime/Plugins/WebGL/EncoreUnity.jslib, over the web SDK's Encore.claim.
    internal sealed partial class WebGLPlatform
    {
        [DllImport("__Internal")] private static extern void EncoreUnity_ClaimCreate(int id, string optionsJson, JsCallback onMessage);
        [DllImport("__Internal")] private static extern void EncoreUnity_ClaimDestroy(int id);
        [DllImport("__Internal")] private static extern void EncoreUnity_ClaimSetVisible(int id, double fraction);
        [DllImport("__Internal")] private static extern void EncoreUnity_ClaimTap(int id, string target, int requestId, JsCallback onResult);
        [DllImport("__Internal")] private static extern void EncoreUnity_ClaimDismiss(int id);
        [DllImport("__Internal")] private static extern void EncoreUnity_ClaimSetSheetText(int id, string json);

        private static readonly JsCallback ClaimCallback = OnClaimMessage;
        private static readonly Dictionary<int, Action<string>> Claims = new Dictionary<int, Action<string>>();

        public void ClaimCreate(long id, string optionsJson, Action<string> onMessageJson)
        {
            Claims[(int)id] = onMessageJson;
            EncoreUnity_ClaimCreate((int)id, optionsJson, ClaimCallback);
        }

        public void ClaimDestroy(long id)
        {
            Claims.Remove((int)id);
            EncoreUnity_ClaimDestroy((int)id);
        }

        public void ClaimSetVisible(long id, double fraction) => EncoreUnity_ClaimSetVisible((int)id, fraction);

        public void ClaimTap(long id, string target, Action<string> onResultJson)
        {
            var requestId = ++_nextRequestId;
            Pending[requestId] = onResultJson;
            EncoreUnity_ClaimTap((int)id, target ?? "", requestId, ResultCallback);
        }

        public void ClaimDismiss(long id) => EncoreUnity_ClaimDismiss((int)id);

        // Web SDK 2.6.0 takes the prize only when the claim is made, so a later change does not reach it.
        public void ClaimSetPublisherReward(long id, string json) { }

        public void ClaimSetSheetText(long id, string json) => EncoreUnity_ClaimSetSheetText((int)id, json);

        [MonoPInvokeCallback(typeof(JsCallback))]
        private static void OnClaimMessage(int id, IntPtr json)
        {
            if (Claims.TryGetValue(id, out var handler)) handler(Marshal.PtrToStringUTF8(json));
        }
    }
}
#endif
