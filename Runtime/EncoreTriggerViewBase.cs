using System;
using EncoreKit.Internal;
using UnityEngine;

namespace EncoreKit
{
    // Puts a native trigger over this UI element and keeps it there as the layout moves. The native trigger draws above
    // all Unity content, so leave this element empty. EncoreTriggerView, EncoreGiftsButtonView and EncoreClaimReminderView use it.
    [RequireComponent(typeof(RectTransform))]
    public abstract class EncoreTriggerViewBase : MonoBehaviour
    {
        [SerializeField] private string placement = "home";
        [SerializeField] private string headline;
        [SerializeField] private string subheadline;
        [SerializeField] private string cta;
        [Tooltip("The headline of the sheet a tap opens. Empty keeps the sheet's own.")]
        [SerializeField] private string sheetHeadline;
        [SerializeField] private string sheetSubheadline;
        [SerializeField] private TriggerIcon icon = TriggerIcon.Gift;
        [Tooltip("Where the card sits across this element's width. SetAlignment on the trigger changes it later.")]
        [SerializeField] private EncoreAlignment alignment = EncoreAlignment.Center;
        [Tooltip("Sets this element's height to the trigger's: zero when there is nothing to show, or while the look is presented over the screen.")]
        [SerializeField] private bool resizeToContent = true;

        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform _rect;
        private Canvas _canvas;

        public EncoreTrigger Trigger { get; private set; }

        // Read when the trigger is created, on the first frame after Encore.Configure; later changes are ignored.
        public string Placement
        {
            get => placement;
            set => placement = value;
        }

        // The result of each sheet the trigger opens.
        public event Action<PresentationResult> Result;

        // Called once, after Encore.Configure.
        protected abstract EncoreTrigger CreateTrigger();

        // The options this component's fields set.
        protected T Options<T>() where T : TriggerOptions, new() => new T
        {
            Headline = Blank(headline),
            Subheadline = Blank(subheadline),
            Cta = Blank(cta),
            SheetHeadline = Blank(sheetHeadline),
            SheetSubheadline = Blank(sheetSubheadline),
            Icon = icon,
            Alignment = alignment,
        };

        protected virtual void Awake()
        {
            _rect = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        }

        protected virtual void OnEnable() => Trigger?.SetVisible(true);

        protected virtual void OnDisable() => Trigger?.SetVisible(false);

        protected virtual void OnDestroy() => Trigger?.Dispose();

        protected virtual void LateUpdate()
        {
            // Waits for Encore.Configure, so the component can sit in the first scene.
            if (Trigger == null)
            {
                if (!Encore.IsConfigured) return;
                try
                {
                    Trigger = CreateTrigger();
                }
                catch (ArgumentException e)
                {
                    // A blank id or placement in the Inspector: say so once, not every frame. Logged at every level, because it
                    // stands in for the ArgumentException a code call would throw.
                    Debug.LogError($"[Encore] {name}: {e.Message}. The {GetType().Name} is disabled.", this);
                    enabled = false;
                    return;
                }
                Trigger.Result += result => MainThread.Raise(Result, result);
            }
            // Every frame, not only on HeightChanged: a game layout pass can reset this element's height.
            if (resizeToContent) ApplyHeight(Trigger.Height);
            Trigger.SetFrame(ScreenRect());
        }

        private Rect ScreenRect()
        {
            _rect.GetWorldCorners(_corners);
            var camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, _corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, _corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void ApplyHeight(float screenHeight)
        {
            var scale = _canvas != null ? _canvas.scaleFactor : 1f;
            var height = screenHeight / scale;
            if (!Mathf.Approximately(_rect.rect.height, height)) _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private static string Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
