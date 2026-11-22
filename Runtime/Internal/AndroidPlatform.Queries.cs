#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace EncoreKit.Internal
{
    // Calls EncoreUnityQueries in the AAR.
    internal sealed partial class AndroidPlatform
    {
        private readonly AndroidJavaClass _queries = new AndroidJavaClass("com.encorekit.unity.EncoreUnityQueries");

        public void SetClaimEnabled(bool enabled) => _queries.CallStatic("setClaimEnabled", enabled);

        // False from the bridge means the native SDK did not take it (older than 2.7.0): throw, so Configure fails closed
        // and IsEnabled keeps the value the SDK still has.
        public void SetEnabled(bool enabled)
        {
            if (!_queries.CallStatic<bool>("setEnabled", enabled))
                throw new InvalidOperationException("The native Android SDK did not take isEnabled");
        }

        public void SetPublisherRewardDailyLimit(int dailyLimit) => _queries.CallStatic("setPublisherRewardDailyLimit", dailyLimit);

        // The Android SDK 2.5.0 has no placement prefetch.
        public void Prefetch(string requestJson) { }

        public void HasOffers(string requestJson, Action<string> onReplyJson) =>
            _queries.CallStatic("hasOffers", requestJson, new Callback(onReplyJson));

        public void SetWrapper(string name, string version) => _queries.CallStatic("setWrapper", name, version);

        public void ReportWrapperError(string where, string code) => _queries.CallStatic("reportWrapperError", where, code);
    }
}
#endif
