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
