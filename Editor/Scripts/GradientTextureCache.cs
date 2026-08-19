using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    public class CachedTextureData
    {
        public Texture2D ForwardTexture;
        public Texture2D BackwardTexture;
    }

    /// <summary>
    /// Shared cache of horizontal fade-out gradient textures (color -> fadeTo color), reused by
    /// both the Project window and Hierarchy window row-coloring overlays.
    /// </summary>
    public static class GradientTextureCache
    {
        private const int TextureSize = 64;
        private static readonly Dictionary<string, CachedTextureData> _cache = new();

        public static CachedTextureData GetOrCreate(Color color, Color fadeTo)
        {
            string key = ColorUtility.ToHtmlStringRGBA(color) + "_" + ColorUtility.ToHtmlStringRGBA(fadeTo);
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            cached = new CachedTextureData
            {
                ForwardTexture = Make(Vector2.right, color, fadeTo),
                BackwardTexture = Make(Vector2.left, color, fadeTo)
            };
            _cache[key] = cached;
            return cached;
        }

        private static Texture2D Make(Vector2 direction, Color startColor, Color endColor)
        {
            Texture2D texture = new Texture2D(TextureSize, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            try
            {
                Color[] pixels = new Color[TextureSize];
                direction.Normalize();
                float minDot = float.MaxValue, maxDot = float.MinValue;

                for (int x = 0; x < TextureSize; x++)
                {
                    float dot = Vector2.Dot(new Vector2(x, 0), direction);
                    if (dot <= minDot) minDot = dot;
                    if (dot > maxDot) maxDot = dot;
                }

                float dotRange = maxDot - minDot;
                for (int x = 0; x < TextureSize; x++)
                {
                    float t = Mathf.Clamp01((Vector2.Dot(new Vector2(x, 0), direction) - minDot) / dotRange);
                    float eased = t * t * (3f - 2f * t); // smoothstep: ends softer than a linear falloff
                    pixels[x] = Color.Lerp(startColor, endColor, eased);
                }

                texture.SetPixels(pixels);
                texture.Apply();
                return texture;
            }
            catch { return texture; }
        }
    }
}
