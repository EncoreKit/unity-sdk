using System;
using System.Collections.Generic;
using System.Threading;
using EncoreKit.Internal;
using UnityEngine;

namespace EncoreKit
{
    public class TriggerOptions
    {
        public UseCase UseCase = UseCase.RewardUsers;
        // Forbids a look that opens its sheet by itself. Unset, Encore decides per served look.
        public bool AutoOpenOff;
        // Your copy in place of the look's own; null keeps the look's text.
        public string Headline;
        public string Subheadline;
        public string Cta;
        // Your own words and numbers for the look to draw, by the names the look uses.
        public Dictionary<string, string> Values;
        public TriggerIcon Icon = TriggerIcon.Gift;
        // Where the card sits across the width SetFrame gives.
        public EncoreAlignment Alignment = EncoreAlignment.Center;
        public AppearanceConfig Appearance;
        // Identified users only. Grant it as for Placement.PublisherReward.
        public PublisherReward PublisherReward;
    }

    // A native trigger view, drawn above the game at a screen rect you give. GiftsButton and ClaimReminder are two of them.
    // For a trigger that follows a UI element, add EncoreTriggerView to that element instead.
    public class EncoreTrigger : IDisposable
    {
        private static long _nextId;

        internal static long NextId() => Interlocked.Increment(ref _nextId);

        private readonly long _id;
        // Null before Configure, or when the native create failed: the trigger then shows nothing.
        private readonly IEncorePlatform _platform;
        private Rect _frame;
        private int _disposed;

        private bool Disposed => Volatile.Read(ref _disposed) != 0;

        // In Unity screen pixels. Zero while there is nothing to show: no offers, a holdout, or an error.
        public float Height { get; private set; }

        public event Action<float> HeightChanged;

        // The result of each sheet the trigger opens, from a tap or an automatic open.
        public event Action<PresentationResult> Result;

        private protected EncoreTrigger(string triggerId, string placement, TriggerOptions options)
        {
            if (string.IsNullOrWhiteSpace(placement)) throw new ArgumentException("placement is required", nameof(placement));
            _id = NextId();
            var platform = Encore.Platform;
            if (platform == null)
            {
                Log.Warn($"{GetType().Name} created before Encore.Configure; it shows nothing.");
                return;
            }
            var json = Wire.TriggerOptions(triggerId, placement, options ?? new TriggerOptions());
            if (Log.Contain("CreateGiftsButton", () => platform.CreateGiftsButton(_id, json, OnMessage))) _platform = platform;
            // A create that failed part way may have left a view or an entry behind: remove it.
            else Log.Contain("DestroyGiftsButton", () => platform.DestroyGiftsButton(_id));
        }

        // The id names the trigger, as in the native SDKs: "giftsButton", "claimReminder". An id the native SDK does not know shows nothing.
        public static EncoreTrigger Create(string triggerId, string placement, TriggerOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(triggerId)) throw new ArgumentException("triggerId is required", nameof(triggerId));
            return new EncoreTrigger(triggerId, placement, options);
        }

        // A rect in Unity screen space (origin bottom-left), as Screen and RectTransformUtility use.
        // Only x, y and width are used: the trigger takes its own height and reports it through HeightChanged.
        public void SetFrame(Rect screenRect)
        {
            if (Disposed || _platform == null || screenRect == _frame) return;
            // A NaN never equals the last frame, so it would be sent, and fail natively, every frame.
            if (!IsFinite(screenRect)) return;
            float x = screenRect.xMin, y = Screen.height - screenRect.yMax, width = screenRect.width, w = Screen.width, h = Screen.height;
            // Kept only on success, so a frame the native side failed to take is sent again next frame.
            if (Log.Contain("SetGiftsButtonFrame", () => _platform.SetGiftsButtonFrame(_id, x, y, width, w, h))) _frame = screenRect;
        }

        private static bool IsFinite(Rect r) =>
            !float.IsNaN(r.x) && !float.IsInfinity(r.x) && !float.IsNaN(r.y) && !float.IsInfinity(r.y) &&
            !float.IsNaN(r.width) && !float.IsInfinity(r.width) && !float.IsNaN(r.height) && !float.IsInfinity(r.height);

        public void SetVisible(bool visible)
        {
            if (Disposed || _platform == null) return;
            Log.Contain("SetGiftsButtonVisible", () => _platform.SetGiftsButtonVisible(_id, visible));
        }

        // Redraws in place. Null falls back to the look's own text.
        public void SetText(string headline, string subheadline = null, string cta = null)
        {
            if (Disposed || _platform == null) return;
            SendUpdate(Wire.GiftsButtonTextUpdate(headline, subheadline, cta));
        }

        // Redraws in place and leaves the text as it is. On WebGL it mounts the trigger again.
        public void SetValues(IDictionary<string, string> values)
        {
            if (Disposed || _platform == null) return;
            SendUpdate(Wire.GiftsButtonValuesUpdate(values ?? new Dictionary<string, string>()));
        }

        public void SetAlignment(EncoreAlignment alignment)
        {
            if (Disposed || _platform == null) return;
            SendUpdate(Wire.GiftsButtonAlignmentUpdate(alignment));
        }

        // Redraws in place, or on WebGL mounts the trigger again. Null goes back to the look's own colours.
        public void SetAppearance(AppearanceConfig appearance)
        {
            if (Disposed || _platform == null) return;
            SendUpdate(Wire.GiftsButtonAppearanceUpdate(appearance));
        }

        // Null removes the prize. On WebGL it mounts the trigger again.
        public void SetPublisherReward(PublisherReward reward)
        {
            if (Disposed || _platform == null) return;
            SendUpdate(Wire.GiftsButtonPublisherRewardUpdate(reward));
        }

        // Safe more than once. A message still in flight is dropped. Off the main thread it is tested only against a fake.
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0 || _platform == null) return;
            Log.Contain("DestroyGiftsButton", () => _platform.DestroyGiftsButton(_id));
        }

        private void SendUpdate(string updateJson) => Log.Contain("UpdateGiftsButton", () => _platform.UpdateGiftsButton(_id, updateJson));

        private void OnMessage(string json)
        {
            var message = Wire.ParseGiftsButtonMessage(json);
            if (message == null) return;
            var m = message.Value;
            MainThread.Post(() =>
            {
                if (Disposed) return;
                if (m.IsHeight)
                {
                    if (Mathf.Approximately(m.Height, Height)) return;
                    Height = m.Height;
                    MainThread.Raise(HeightChanged, Height);
                }
                else
                {
                    MainThread.Raise(Result, m.Result);
                }
            });
        }
    }
}
