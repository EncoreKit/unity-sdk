using System;

namespace EncoreKit.Internal
{
    internal partial interface IEncorePlatform
    {
        void SetClaimEnabled(bool enabled);
        void SetPublisherRewardDailyLimit(int dailyLimit);
        void Prefetch(string requestJson);
        // One reply, Wire.ParseHasOffers.
        void HasOffers(string requestJson, Action<string> onReplyJson);
        // Before Configure: names this package on every later analytics event and error report.
        void SetWrapper(string name, string version);
        // A failure C# contained. Both are fixed text: a call name and an exception type name, never a message.
        void ReportWrapperError(string where, string code);
    }
}
