using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Loads the rounded-square design texture from Editor/Resources and caches it so it
    /// can be shared across multiple scripts (Inspector, popup, etc.).
    /// </summary>
    public static class RoundedTextureProvider
    {
        private const string ResourceName = "rounded_square_icon";
        private static Texture2D _texture;

        public static Texture2D Get()
        {
            if (_texture != null)
                return _texture;

            _texture = Resources.Load<Texture2D>(ResourceName);

            if (_texture == null)
            {
                Debug.LogWarning($"Texture '{ResourceName}' was not found under Resources. " +
                                 "Make sure it's in the Editor/Resources folder and the file name is spelled correctly.");
            }

            return _texture;
        }
    }
}