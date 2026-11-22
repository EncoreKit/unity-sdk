using System;

namespace EncoreKit.Internal
{
    // No native SDK in the Editor, so a claim stays hidden and a tap opens nothing.
    internal sealed partial class EditorPlatform
    {
        private const string NoOffers = "{\"type\":\"notPresented\",\"reason\":\"no_offer_available\"}";

        public void ClaimCreate(long id, string optionsJson, Action<string> onMessageJson) { }
        public void ClaimDestroy(long id) { }
        public void ClaimSetVisible(long id, double fraction) { }
        public void ClaimTap(long id, string target, Action<string> onResultJson) => onResultJson(NoOffers);
        public void ClaimDismiss(long id) { }
        public void ClaimSetPublisherReward(long id, string json) { }
        public void ClaimSetSheetText(long id, string json) { }
    }
}
