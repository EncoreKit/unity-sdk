using System;
using System.Threading.Tasks;
using EncoreKit.Internal;

namespace EncoreKit
{
    // Built by Encore.Placement(id). Every runtime failure is a value on the result; only a null callback throws.
    public sealed partial class Placement
    {
        private readonly string _placementId;
        // ReduceChurn when UseCase is not called, as in the Swift and Android builders.
        private UseCase _useCase = EncoreKit.UseCase.ReduceChurn;
        private string _headline;
        private string _subheadline;
        private string _cta;
        private string _decline;
        private string _badge;
        private AppearanceConfig _appearance;
        private PublisherReward _publisherReward;

        internal Placement(string placementId) => _placementId = placementId;

        public Placement UseCase(UseCase useCase)
        {
            _useCase = useCase;
            return this;
        }

        public Placement Headline(string headline)
        {
            _headline = headline;
            return this;
        }

        public Placement Subheadline(string subheadline)
        {
            _subheadline = subheadline;
            return this;
        }

        // The primary button's label. Android and iOS; WebGL ignores it. An empty string keeps the served copy.
        public Placement Cta(string cta)
        {
            _cta = cta;
            return this;
        }

        // The decline button's label. Same rules as Cta.
        public Placement Decline(string decline)
        {
            _decline = decline;
            return this;
        }

        // The badge on the highlighted offer. Same rules as Cta.
        public Placement Badge(string badge)
        {
            _badge = badge;
            return this;
        }

        // Last call wins. Android, iOS and WebGL.
        public Placement Appearance(AppearanceConfig appearance)
        {
            _appearance = appearance;
            return this;
        }

        // Identified users only. On WebGL the web SDK 2.5.0 takes it but does not draw or grant it yet.
        public Placement PublisherReward(PublisherReward reward)
        {
            _publisherReward = reward;
            return this;
        }

        public void Show(Action<PresentationResult> onResult)
        {
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));
            onResult = WithLoadingState(onResult);
            string request;
            try
            {
                request = Wire.PlacementRequest(_placementId, _useCase, _headline, _subheadline, _appearance, _publisherReward, _cta, _decline, _badge);
            }
            catch (ArgumentOutOfRangeException)
            {
                MainThread.Post(() => onResult(PresentationResult.NotPresentedBecause(NotPresentedReason.UseCaseUnavailable)));
                return;
            }
            Encore.Show(request, onResult);
        }

        public Task<PresentationResult> ShowAsync()
        {
            var tcs = new TaskCompletionSource<PresentationResult>();
            Show(result => tcs.TrySetResult(result));
            return tcs.Task;
        }
    }
}
