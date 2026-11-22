#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;

namespace EncoreKit.Internal
{
    // Calls Runtime/Plugins/WebGL/EncoreUnityQueries.jslib.
    internal sealed partial class WebGLPlatform
    {
        [DllImport("__Internal")] private static extern void EncoreUnity_SetClaimEnabled(int enabled);
        [DllImport("__Internal")] private static extern int EncoreUnity_SetEnabled(int enabled);
        [DllImport("__Internal")] private static extern void EncoreUnity_Prefetch(string requestJson);
        [DllImport("__Internal")] private static extern void EncoreUnity_HasOffers(int requestId, string requestJson, JsCallback onReply);
        [DllImport("__Internal")] private static extern void EncoreUnity_SetWrapper(string name, string version);
        [DllImport("__Internal")] private static extern void EncoreUnity_ReportWrapperError(string where, string code);

        public void SetClaimEnabled(bool enabled) => EncoreUnity_SetClaimEnabled(enabled ? 1 : 0);

        // The web SDK's isEnabled, 2.7.0 and later. Off that did not take (an older bundled web SDK has no switch) throws, so
        // Configure fails closed and IsEnabled keeps the value the web SDK still has.
        public void SetEnabled(bool enabled)
        {
            if (EncoreUnity_SetEnabled(enabled ? 1 : 0) != 1)
                throw new InvalidOperationException("The bundled web SDK has no isEnabled; it stays on");
        }

        // The web SDK has no reward policy, so the server default applies.
        public void SetPublisherRewardDailyLimit(int dailyLimit) { }

        public void Prefetch(string requestJson) => EncoreUnity_Prefetch(requestJson);

        // The bundled web SDK has hasOffers and the wrapper API from 2.5.0. With an older bundle these answer false and do
        // nothing.
        public void HasOffers(string requestJson, Action<string> onReplyJson)
        {
            var id = ++_nextRequestId;
            Pending[id] = onReplyJson;
            EncoreUnity_HasOffers(id, requestJson, ResultCallback);
        }

        public void SetWrapper(string name, string version) => EncoreUnity_SetWrapper(name, version);

        public void ReportWrapperError(string where, string code) => EncoreUnity_ReportWrapperError(where, code);
    }
}
#endif
