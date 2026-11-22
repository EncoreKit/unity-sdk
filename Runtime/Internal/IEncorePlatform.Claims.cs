using System;

namespace EncoreKit.Internal
{
    // The custom claim (Encore.Claim): one native claim handle per id. Names follow the Flutter claim channel's methods.
    internal partial interface IEncorePlatform
    {
        // optionsJson is Wire.ClaimCreate. onMessageJson gets each state and result, Wire.ParseClaimMessage.
        void ClaimCreate(long id, string optionsJson, Action<string> onMessageJson);
        // Stops the state at once; a tap in flight is still answered.
        void ClaimDestroy(long id);
        void ClaimSetVisible(long id, double fraction);
        // target is Wire.ClaimTargetValue. One reply, a presentation result.
        void ClaimTap(long id, string target, Action<string> onResultJson);
        void ClaimDismiss(long id);
        void ClaimSetPublisherReward(long id, string json);
        void ClaimSetSheetText(long id, string json);
    }
}
