using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EncoreKit.Internal;

namespace EncoreKit
{
    public sealed class ClaimOptions
    {
        // The sheet a tap opens. ReduceChurn stays hidden for now, as natively.
        public UseCase UseCase = UseCase.RewardUsers;
        // Forbids opening the sheet by itself. Unset, Encore decides per placement.
        public bool AutoOpenOff;
        // Your prize on every sheet a tap opens, as Placement.PublisherReward. Also settable later.
        public PublisherReward PublisherReward;
        // The headline and subheadline of every sheet a tap opens; null or blank keeps the sheet's own. Also settable later.
        public string SheetHeadline;
        public string SheetSubheadline;
    }

    public enum ClaimStateKind
    {
        // Draw nothing: no offers, a holdout, not configured yet, a failure, or a completed claim when Encore hides it.
        Hidden,
        // The sheet has offers: OfferCount and LogoUrls.
        Gifts,
        // An open claim the sheet still lists: AdvertiserName to TransactionId.
        Reminder,
        // After a completed claim, when Encore's rules say so: thank the player. OfferCount and LogoUrls as for Gifts.
        Thanks,
    }

    // What an EncoreClaim says to draw. Fields that do not belong to Kind are empty, zero or null.
    public sealed class ClaimState : IEquatable<ClaimState>
    {
        private static readonly IReadOnlyList<string> NoLogos = Array.Empty<string>();

        public static readonly ClaimState Hidden = new ClaimState(ClaimStateKind.Hidden);

        public ClaimStateKind Kind { get; }
        public int OfferCount { get; private set; }
        // Up to eight logos, one per brand, in the sheet's order.
        public IReadOnlyList<string> LogoUrls { get; private set; } = NoLogos;
        public string AdvertiserName { get; private set; }
        public string LogoUrl { get; private set; }
        // The claimed offer's perk as the sheet showed it, for example "14 days free".
        public string Perk { get; private set; }
        public DateTimeOffset ClaimedAt { get; private set; }
        // When the reminder ends: 24 hours after the claim. The claim itself stays open.
        public DateTimeOffset Deadline { get; private set; }
        // Open claims on show, this one included.
        public int ClaimCount { get; private set; }
        public string TransactionId { get; private set; }

        private ClaimState(ClaimStateKind kind) => Kind = kind;

        internal static ClaimState Offers(ClaimStateKind kind, int offerCount, IReadOnlyList<string> logoUrls) =>
            new ClaimState(kind) { OfferCount = offerCount, LogoUrls = logoUrls ?? NoLogos };

        internal static ClaimState Reminding(string advertiserName, string logoUrl, string perk, DateTimeOffset claimedAt,
            DateTimeOffset deadline, int claimCount, string transactionId) => new ClaimState(ClaimStateKind.Reminder)
        {
            AdvertiserName = advertiserName,
            LogoUrl = logoUrl,
            Perk = perk,
            ClaimedAt = claimedAt,
            Deadline = deadline,
            ClaimCount = claimCount,
            TransactionId = transactionId,
        };

        public bool Equals(ClaimState other)
        {
            if (other == null || other.Kind != Kind || other.OfferCount != OfferCount || other.LogoUrls.Count != LogoUrls.Count) return false;
            for (var i = 0; i < LogoUrls.Count; i++)
            {
                if (other.LogoUrls[i] != LogoUrls[i]) return false;
            }
            return other.AdvertiserName == AdvertiserName && other.LogoUrl == LogoUrl && other.Perk == Perk &&
                   other.ClaimedAt == ClaimedAt && other.Deadline == Deadline && other.ClaimCount == ClaimCount &&
                   other.TransactionId == TransactionId;
        }

        public override bool Equals(object obj) => Equals(obj as ClaimState);
        public override int GetHashCode() => ((int)Kind * 397) ^ OfferCount ^ (TransactionId?.GetHashCode() ?? 0);
        public override string ToString() => $"ClaimState({Kind})";
    }

    // What a tap stands for, as analytics counts it. On gifts and thanks every tap counts as Button; on a reminder Button counts as Card.
    public enum ClaimTarget { Button, Card, Arrow }

    // A gifts button or claim reminder the game draws: Encore decides State, counts the impression and opens the sheet on Tap.
    // Made by Encore.Claim; Dispose it with your view. Android, iOS and WebGL; hidden in the Editor.
    public sealed class EncoreClaim : IDisposable
    {
        private static long _nextId;

        private readonly long _id;
        // Null before Configure, or when the native create failed: the claim then stays hidden.
        private readonly IEncorePlatform _platform;
        private PublisherReward _publisherReward;
        private int _disposed;
        private int _tapping;
        // Made before Encore.Configure: a tap answers NotConfigured, as every other call before Configure does.
        private readonly bool _notConfigured;

        private bool Disposed => Volatile.Read(ref _disposed) != 0;

        public string Placement { get; }

        // Hidden until Encore answers, and after Dispose.
        public ClaimState State { get; private set; } = ClaimState.Hidden;

        // Whether to show your entry point: false while State is hidden.
        public bool HasOffers => State.Kind != ClaimStateKind.Hidden;

        // Each new State, on the main thread.
        public event Action<ClaimState> StateChanged;

        // The result of each sheet the claim opens, from a tap or an automatic open, after it closes.
        public event Action<PresentationResult> Result;

        internal EncoreClaim(string placement, ClaimOptions options)
        {
            if (string.IsNullOrWhiteSpace(placement)) throw new ArgumentException("placement is required", nameof(placement));
            Placement = placement;
            options = options ?? new ClaimOptions();
            _publisherReward = options.PublisherReward;
            _id = Interlocked.Increment(ref _nextId);
            var platform = Encore.Platform;
            if (platform == null)
            {
                Log.Warn("EncoreClaim created before Encore.Configure; it stays hidden.");
                _notConfigured = true;
                return;
            }
            string json;
            try
            {
                json = Wire.ClaimCreate(placement, options);
            }
            catch (ArgumentOutOfRangeException)
            {
                // A use case the bridges do not know: fail closed.
                Log.Warn("EncoreClaim has a use case the native SDKs do not know; it stays hidden.");
                return;
            }
            if (Log.Contain("ClaimCreate", () => platform.ClaimCreate(_id, json, OnMessage))) _platform = platform;
            else Log.Contain("ClaimDestroy", () => platform.ClaimDestroy(_id));
        }

        // Your prize on every sheet a tap opens; null removes it. On WebGL only the value at creation applies.
        public PublisherReward PublisherReward
        {
            get => _publisherReward;
            set
            {
                _publisherReward = value;
                if (Disposed || _platform == null) return;
                var json = Wire.ClaimPublisherReward(value);
                Log.Contain("ClaimSetPublisherReward", () => _platform.ClaimSetPublisherReward(_id, json));
            }
        }

        // The headline and subheadline of every sheet a tap opens from now on; null or blank keeps the sheet's own.
        public void SetSheetText(string headline, string subheadline = null)
        {
            if (Disposed || _platform == null) return;
            var json = Wire.ClaimSheetText(headline, subheadline);
            Log.Contain("ClaimSetSheetText", () => _platform.ClaimSetSheetText(_id, json));
        }

        // The share of your view the player sees, 0 to 1, sent as given (the native claim clamps it; NaN is 0); half for a second
        // counts. Call it as the view shows, moves or hides. The native claim keeps the last share, so Dispose the claim with your view.
        public void SetVisible(float fraction)
        {
            if (Disposed || _platform == null) return;
            Log.Contain("ClaimSetVisible", () => _platform.ClaimSetVisible(_id, fraction));
        }

        // Opens the sheet and answers once it closes, on the main thread; one tap at a time, and a tap in flight at Dispose still
        // answers. NotConfigured before Configure, NoOffers while hidden or after Dispose. Null target: the native default.
        public void Tap(Action<PresentationResult> onResult, ClaimTarget? target = null)
        {
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));
            var platform = _platform;
            if (Disposed || platform == null)
            {
                var reason = _notConfigured && !Disposed ? NotPresentedReason.NotConfigured : NotPresentedReason.NoOffers;
                MainThread.Post(() => onResult(PresentationResult.NotPresentedBecause(reason)));
                return;
            }
            if (Interlocked.Exchange(ref _tapping, 1) != 0)
            {
                MainThread.Post(() => onResult(PresentationResult.NotPresentedBecause(NotPresentedReason.AlreadyPresenting)));
                return;
            }
            var answered = 0;
            void Answer(PresentationResult result)
            {
                if (Interlocked.Exchange(ref answered, 1) != 0) return;
                MainThread.Post(() =>
                {
                    Volatile.Write(ref _tapping, 0);
                    onResult(result);
                });
            }
            try
            {
                platform.ClaimTap(_id, Wire.ClaimTargetValue(target), json => Answer(Wire.ParseResult(json)));
            }
            catch (Exception e)
            {
                Log.Contained("ClaimTap", e);
                Answer(PresentationResult.NotPresentedBecause(NotPresentedReason.Error, e.Message));
            }
        }

        public Task<PresentationResult> TapAsync(ClaimTarget? target = null)
        {
            var tcs = new TaskCompletionSource<PresentationResult>();
            Tap(result => tcs.TrySetResult(result), target);
            return tcs.Task;
        }

        // Hides the reminder, every claim it was showing, on this device. Does nothing unless State is a reminder.
        public void Dismiss()
        {
            if (Disposed || _platform == null || State.Kind != ClaimStateKind.Reminder) return;
            Log.Contain("ClaimDismiss", () => _platform.ClaimDismiss(_id));
        }

        // Releases the native claim; State goes back to hidden with no event. Safe more than once.
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0 || _platform == null) return;
            State = ClaimState.Hidden;
            Log.Contain("ClaimDestroy", () => _platform.ClaimDestroy(_id));
        }

        private void OnMessage(string json)
        {
            var message = Wire.ParseClaimMessage(json);
            if (message == null) return;
            var m = message.Value;
            MainThread.Post(() =>
            {
                if (Disposed) return;
                if (m.State != null)
                {
                    // Again here, as Dispose may have run on another thread since the check above.
                    if (m.State.Equals(State) || Disposed) return;
                    State = m.State;
                    MainThread.Raise(StateChanged, State);
                }
                else
                {
                    MainThread.Raise(Result, m.Result);
                }
            });
        }
    }

    public static partial class Encore
    {
        // A handle for your own gifts button or claim reminder at placement, for when GiftsButton and ClaimReminder do not
        // fit your design. Keep it while your view exists and Dispose it with the view. Only a blank placement throws.
        public static EncoreClaim Claim(string placement, ClaimOptions options = null) => new EncoreClaim(placement, options);
    }
}
