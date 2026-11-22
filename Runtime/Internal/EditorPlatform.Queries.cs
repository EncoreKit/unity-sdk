using System;

namespace EncoreKit.Internal
{
    // No native SDK in the Editor, so the settings, the prefetch and the wrapper calls do nothing, and HasOffers is false.
    internal sealed partial class EditorPlatform
    {
        public void SetClaimEnabled(bool enabled) { }
        public void SetEnabled(bool enabled) { }
        public void SetPublisherRewardDailyLimit(int dailyLimit) { }
        public void Prefetch(string requestJson) { }
        public void HasOffers(string requestJson, Action<string> onReplyJson) => onReplyJson("{\"hasOffers\":false}");
        public void SetWrapper(string name, string version) { }
        public void ReportWrapperError(string where, string code) { }
    }
}
