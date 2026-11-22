using System;
using EncoreKit.Internal;
using UnityEngine;

namespace EncoreKit
{
    // Puts the native gift button over this UI element and keeps it there as the layout moves.
    // The native button draws above all Unity content, so leave this element empty.
    [AddComponentMenu("Encore/Gifts Button")]
    public sealed class EncoreGiftsButtonView : EncoreTriggerViewBase
    {
        [SerializeField] private bool autoOpenOff;

        private GiftsButtonHostState _hostState;

        public GiftsButton Button => (GiftsButton)Trigger;

        // Your app's state for this gift, settable any time: before the button exists it goes into its create options.
        public GiftsButtonHostState HostState
        {
            get => _hostState;
            set
            {
                _hostState = value;
                Button?.SetHostState(value);
            }
        }

        // As GiftsButton.ShowingChanged.
        public event Action<bool> ShowingChanged;

        protected override EncoreTrigger CreateTrigger()
        {
            var options = Options<GiftsButtonOptions>();
            options.AutoOpenOff = autoOpenOff;
            options.HostState = _hostState;
            var button = GiftsButton.Create(Placement, options);
            button.ShowingChanged += showing => MainThread.Raise(ShowingChanged, showing);
            return button;
        }
    }
}
