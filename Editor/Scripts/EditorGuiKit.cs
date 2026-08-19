using UnityEditor;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>Procedural drawing helpers for the package's flat card-based Inspector UI.</summary>
    internal static class EditorGuiKit
    {
        public static void DrawRoundedRect(Rect rect, Color fill, float radius)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                fill, Vector4.zero, new Vector4(radius, radius, radius, radius));
        }

        public static void DrawRoundedRect(Rect rect, Color fill, Color border, float borderWidth, float radius)
        {
            if (Event.current.type != EventType.Repaint) return;
            DrawRoundedRect(rect, border, radius);
            Rect inner = new Rect(rect.x + borderWidth, rect.y + borderWidth,
                rect.width - borderWidth * 2f, rect.height - borderWidth * 2f);
            DrawRoundedRect(inner, fill, Mathf.Max(0f, radius - borderWidth));
        }

        /// <summary>Starts a GUILayout vertical group with a card background. Pair with EditorGUILayout.EndVertical().</summary>
        public static Rect BeginCard(GUIStyle layoutStyle, Color fill, Color border, float radius)
        {
            Rect rect = EditorGUILayout.BeginVertical(layoutStyle);
            DrawRoundedRect(rect, fill, border, 1f, radius);
            return rect;
        }
    }
}
