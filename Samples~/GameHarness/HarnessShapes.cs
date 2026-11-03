using System.Collections.Generic;
using UnityEngine;

namespace EncoreKit.GameHarness
{
    // Draws the harness's sprites at runtime, so the sample ships no art files.
    internal static class HarnessShapes
    {
        private static readonly Dictionary<int, Sprite> Rounded = new Dictionary<int, Sprite>();
        private static Sprite _circle;

        // A white rounded rect, 9-sliced so any size keeps its corner radius.
        internal static Sprite RoundedRect(int radius)
        {
            if (Rounded.TryGetValue(radius, out var cached)) return cached;
            var size = radius * 2 + 4;
            var texture = NewTexture(size);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = Mathf.Max(0, Mathf.Abs(x + 0.5f - size / 2f) - (size / 2f - radius));
                var dy = Mathf.Max(0, Mathf.Abs(y + 0.5f - size / 2f) - (size / 2f - radius));
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f)));
            }
            texture.Apply();
            var border = new Vector4(radius + 1, radius + 1, radius + 1, radius + 1);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
            Rounded[radius] = sprite;
            return sprite;
        }

        internal static Sprite Circle()
        {
            if (_circle != null) return _circle;
            const int size = 128;
            var texture = NewTexture(size);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(size / 2f - d)));
            }
            texture.Apply();
            _circle = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _circle;
        }

        private static Texture2D NewTexture(int size) =>
            new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
    }
}
