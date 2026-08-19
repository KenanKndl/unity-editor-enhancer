using System;
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
        private static readonly Dictionary<ColorPairKey, CachedTextureData> _cache = new();

        public static CachedTextureData GetOrCreate(Color color, Color fadeTo)
        {
            var key = new ColorPairKey(color, fadeTo);
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

        // Packed uint keys avoid a per-lookup string allocation - GetOrCreate runs on every
        // colored row/folder repaint.
        private readonly struct ColorPairKey : IEquatable<ColorPairKey>
        {
            private readonly uint _from;
            private readonly uint _to;

            public ColorPairKey(Color from, Color to)
            {
                _from = Pack(from);
                _to = Pack(to);
            }

            private static uint Pack(Color c)
            {
                Color32 c32 = c;
                return ((uint)c32.r << 24) | ((uint)c32.g << 16) | ((uint)c32.b << 8) | c32.a;
            }

            public bool Equals(ColorPairKey other) => _from == other._from && _to == other._to;
            public override bool Equals(object obj) => obj is ColorPairKey other && Equals(other);
            public override int GetHashCode() => unchecked((int)(_from * 397 ^ _to));
        }
    }
}
