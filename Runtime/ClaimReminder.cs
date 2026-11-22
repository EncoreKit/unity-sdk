namespace EncoreKit
{
    // A native banner that reminds the user of an offer they claimed but have not finished, drawn above the game at a
    // screen rect you give. Tapping it opens the sheet that holds that offer. Height stays zero while there is no
    // claim, and while the look is presented over the screen.
    // For a banner that follows a UI element, add EncoreClaimReminderView to that element instead.
    public sealed class ClaimReminder : EncoreTrigger
    {
        internal const string TriggerId = "claimReminder";

        private ClaimReminder(string placement, TriggerOptions options) : base(TriggerId, placement, options)
        {
        }

        public static ClaimReminder Create(string placement, TriggerOptions options = null) => new ClaimReminder(placement, options);
    }
}
