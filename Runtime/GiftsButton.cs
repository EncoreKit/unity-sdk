using System;
using EncoreKit.Internal;

namespace EncoreKit
{
    public sealed class GiftsButtonOptions : TriggerOptions
    {
        // Your app's state for this gift. Null (the default) is as without one. Android and iOS; WebGL ignores it.
        public GiftsButtonHostState HostState;
    }

    public enum GiftsButtonHostStatus
    {
        // Not yet: a tap only shows a hint, and the sheet never opens.
        Locked,
        // A tap opens the sheet.
        Ready,
        // Opened already; the look draws its done state.
        Done,
    }

    // What your app knows about the gift behind a GiftsButton: locked until something in the game happens, then ready, then done.
    // The bridge passes it all to the native SDK, which sends only the status to Encore, on trigger events. The progress stays on the device.
    public sealed class GiftsButtonHostState : IEquatable<GiftsButtonHostState>
    {
        public GiftsButtonHostStatus Status { get; }
        // 0 to 1 toward unlocking, clamped; null when none was given, or for NaN or an infinity.
        public float? Progress { get; }
        // The steps Locked(completed, total) was given, so the native look fills exactly the completed ring segments.
        internal int? Completed { get; }
        internal int? Total { get; }

        private GiftsButtonHostState(GiftsButtonHostStatus status, float? progress, int? completed = null, int? total = null)
        {
            Status = status;
            Progress = progress.HasValue && !float.IsNaN(progress.Value) && !float.IsInfinity(progress.Value)
                ? Math.Min(Math.Max(progress.Value, 0f), 1f)
                : (float?)null;
            Completed = completed;
            Total = total;
        }

        public static GiftsButtonHostState Locked(float? progress = null) => new GiftsButtonHostState(GiftsButtonHostStatus.Locked, progress);

        // Locked with progress as steps: Locked(3, 4) draws as Locked(0.75f). A total of 0 or less is no progress; completed is held to 0 to total.
        public static GiftsButtonHostState Locked(int completed, int total)
        {
            if (total <= 0) return Locked();
            var held = Math.Min(Math.Max(completed, 0), total);
            return new GiftsButtonHostState(GiftsButtonHostStatus.Locked, (float)held / total, held, total);
        }

        public static readonly GiftsButtonHostState Ready = new GiftsButtonHostState(GiftsButtonHostStatus.Ready, null);
        public static readonly GiftsButtonHostState Done = new GiftsButtonHostState(GiftsButtonHostStatus.Done, null);

        // Status and progress only, as on Android: the native look draws the ratio, so Locked(1, 2) is Locked(2, 4).
        public bool Equals(GiftsButtonHostState other) =>
            other != null && other.Status == Status && other.Progress == Progress;

        public override bool Equals(object obj) => Equals(obj as GiftsButtonHostState);
        public override int GetHashCode() => ((int)Status * 397) ^ Progress.GetHashCode();

        // The status only: the progress never reaches a log.
        public override string ToString() => $"GiftsButtonHostState({Status})";
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

        // True while a look is drawn, false while it draws nothing: on the first answer, then on each change. Android and iOS.
        public event Action<bool> ShowingChanged;

        // Redraws in place, with no new request. Null goes back to no host state. Android and iOS; WebGL ignores it.
        public void SetHostState(GiftsButtonHostState state) => Update(Wire.GiftsButtonHostStateUpdate(state));

        private protected override void OnShowing(bool showing) => MainThread.Raise(ShowingChanged, showing);
    }
}
