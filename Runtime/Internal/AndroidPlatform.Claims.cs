#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace EncoreKit.Internal
{
    // Calls EncoreUnityClaim in the AAR.
    internal sealed partial class AndroidPlatform
    {
        private readonly AndroidJavaClass _claim = new AndroidJavaClass("com.encorekit.unity.EncoreUnityClaim");

        public void ClaimCreate(long id, string optionsJson, Action<string> onMessageJson) =>
            _claim.CallStatic("create", id, optionsJson, new Callback(onMessageJson));

        public void ClaimDestroy(long id) => _claim.CallStatic("destroy", id);

        public void ClaimSetVisible(long id, double fraction) => _claim.CallStatic("setVisible", id, fraction);

        public void ClaimTap(long id, string target, Action<string> onResultJson) =>
            _claim.CallStatic("tap", id, target ?? "", new Callback(onResultJson));

        public void ClaimDismiss(long id) => _claim.CallStatic("dismiss", id);

        public void ClaimSetPublisherReward(long id, string json) => _claim.CallStatic("setPublisherReward", id, json);

        public void ClaimSetSheetText(long id, string json) => _claim.CallStatic("setSheetText", id, json);
    }
}
#endif
