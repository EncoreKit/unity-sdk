using UnityEngine;

namespace EncoreKit
{
    // Puts the native trigger with the id you give over this UI element and keeps it there as the layout moves.
    // The native trigger draws above all Unity content, so leave this element empty.
    [AddComponentMenu("Encore/Trigger")]
    public sealed class EncoreTriggerView : EncoreTriggerViewBase
    {
        [Tooltip("The native trigger id, such as giftsButton or claimReminder. An id the native SDK does not know shows nothing.")]
        [SerializeField] private string triggerId = GiftsButton.TriggerId;
        [SerializeField] private bool autoOpenOff;

        // Read when the trigger is created, as Placement is.
        public string TriggerId
        {
            get => triggerId;
            set => triggerId = value;
        }

        protected override EncoreTrigger CreateTrigger()
        {
            var options = Options<TriggerOptions>();
            options.AutoOpenOff = autoOpenOff;
            return EncoreTrigger.Create(triggerId, Placement, options);
        }
    }
}
