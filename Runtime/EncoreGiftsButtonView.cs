using UnityEngine;

namespace EncoreKit
{
    // Puts the native gift button over this UI element and keeps it there as the layout moves.
    // The native button draws above all Unity content, so leave this element empty.
    [AddComponentMenu("Encore/Gifts Button")]
    public sealed class EncoreGiftsButtonView : EncoreTriggerViewBase
    {
        [SerializeField] private bool autoOpenOff;

        public GiftsButton Button => (GiftsButton)Trigger;

        protected override EncoreTrigger CreateTrigger()
        {
            var options = Options<GiftsButtonOptions>();
            options.AutoOpenOff = autoOpenOff;
            return GiftsButton.Create(Placement, options);
        }
    }
}
