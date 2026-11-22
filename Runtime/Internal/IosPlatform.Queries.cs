#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;

namespace EncoreKit.Internal
{
    // Calls the @_cdecl functions in Runtime/Plugins/iOS/EncoreUnityQueries.swift.
    internal sealed partial class IosPlatform
    {
        [DllImport("__Internal")] private static extern void EncoreUnity_SetClaimEnabled([MarshalAs(UnmanagedType.I1)] bool enabled);
        [DllImport("__Internal")] private static extern void EncoreUnity_SetEnabled([MarshalAs(UnmanagedType.I1)] bool enabled);
        [DllImport("__Internal")] private static extern void EncoreUnity_SetPublisherRewardDailyLimit(int dailyLimit);
        [DllImport("__Internal")] private static extern void EncoreUnity_Prefetch(string requestJson);
        [DllImport("__Internal")] private static extern void EncoreUnity_HasOffers(long requestId, string requestJson, NativeCallback onReply);
        [DllImport("__Internal")] private static extern void EncoreUnity_SetWrapper(string name, string version);
        [DllImport("__Internal")] private static extern void EncoreUnity_ReportWrapperError(string where, string code);

        public void SetClaimEnabled(bool enabled) => EncoreUnity_SetClaimEnabled(enabled);
        public void SetEnabled(bool enabled) => EncoreUnity_SetEnabled(enabled);
        public void SetPublisherRewardDailyLimit(int dailyLimit) => EncoreUnity_SetPublisherRewardDailyLimit(dailyLimit);
        public void Prefetch(string requestJson) => EncoreUnity_Prefetch(requestJson);

        public void HasOffers(string requestJson, Action<string> onReplyJson) =>
            EncoreUnity_HasOffers(Register(onReplyJson), requestJson, ResultCallback);

        public void SetWrapper(string name, string version) => EncoreUnity_SetWrapper(name, version);

        public void ReportWrapperError(string where, string code) => EncoreUnity_ReportWrapperError(where, code);
    }
}
#endif
