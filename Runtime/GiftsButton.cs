namespace EncoreKit
{
    public sealed class GiftsButtonOptions : TriggerOptions
    {
    }

    // The native gift button, drawn above the game at a screen rect you give. Tapping it opens the reward sheet.
    // For a button that follows a UI element, add EncoreGiftsButtonView to that element instead.
    public sealed class GiftsButton : EncoreTrigger
    {
        internal const string TriggerId = "giftsButton";

        private GiftsButton(string placement, GiftsButtonOptions options) : base(TriggerId, placement, options)
        {
        }

        public static GiftsButton Create(string placement, GiftsButtonOptions options = null) => new GiftsButton(placement, options);
    }
}
