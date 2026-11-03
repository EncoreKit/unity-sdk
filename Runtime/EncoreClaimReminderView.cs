using UnityEngine;

namespace EncoreKit
{
    // Puts the native claim reminder over this UI element and keeps it there as the layout moves. It has no height
    // while there is no claim to finish. The native banner draws above all Unity content, so leave this element empty.
    [AddComponentMenu("Encore/Claim Reminder")]
    public sealed class EncoreClaimReminderView : EncoreTriggerViewBase
    {
        public ClaimReminder Reminder => (ClaimReminder)Trigger;

        protected override EncoreTrigger CreateTrigger() => ClaimReminder.Create(Placement, Options<TriggerOptions>());
    }
}
