using UnityEngine;

namespace AfterHours.UI
{
    /// <summary>
    /// One look for every menu and on-screen word: dark translucent panels, quiet off-white text, flat buttons that
    /// brighten under the mouse. Built in code (no assets). Scale with UiSkin.Scale() so it's the same size at any
    /// resolution (designed at 1080p).
    /// </summary>
    public static class UiSkin
    {
        static GUISkin skin;
        static Texture2D panel, button, buttonHover, buttonActive, field, sliderBar, thumb;

        public static readonly Color Text = new Color(0.86f, 0.85f, 0.8f);
        public static readonly Color Dim = new Color(0.55f, 0.54f, 0.5f);

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        public static GUISkin Get()
        {
            if (skin != null) return skin;
            panel = Tex(new Color(0.03f, 0.03f, 0.035f, 0.88f));
            button = Tex(new Color(1f, 1f, 1f, 0.05f));
            buttonHover = Tex(new Color(1f, 1f, 1f, 0.12f));
            buttonActive = Tex(new Color(1f, 1f, 1f, 0.2f));
            field = Tex(new Color(0f, 0f, 0f, 0.5f));
            sliderBar = Tex(new Color(1f, 1f, 1f, 0.15f));
            thumb = Tex(new Color(0.86f, 0.85f, 0.8f, 0.9f));

            skin = Object.Instantiate(GUI.skin);
            skin.hideFlags = HideFlags.DontSave;
            skin.box.normal.background = panel;
            skin.box.border = new RectOffset(0, 0, 0, 0);
            skin.box.padding = new RectOffset(28, 28, 22, 22);
            skin.label.fontSize = 19;
            skin.label.normal.textColor = Text;
            skin.label.wordWrap = true;
            foreach (GUIStyle b in new[] { skin.button, skin.toggle })
            {
                b.normal.background = button;
                b.hover.background = buttonHover;
                b.active.background = buttonActive;
                b.onNormal.background = buttonActive;
                b.onHover.background = buttonActive;
                b.onActive.background = buttonActive;
                b.normal.textColor = b.hover.textColor = b.active.textColor = Text;
                b.onNormal.textColor = b.onHover.textColor = b.onActive.textColor = Color.white;
                b.fontSize = 19;
                b.alignment = TextAnchor.MiddleCenter;
                b.border = new RectOffset(0, 0, 0, 0);
            }
            skin.textField.normal.background = field;
            skin.textField.focused.background = field;
            skin.textField.normal.textColor = Text;
            skin.textField.focused.textColor = Color.white;
            skin.textField.fontSize = 19;
            skin.horizontalSlider.normal.background = sliderBar;
            skin.horizontalSlider.fixedHeight = 4f;
            skin.horizontalSlider.margin = new RectOffset(4, 4, 10, 10);
            skin.horizontalSliderThumb.normal.background = thumb;
            skin.horizontalSliderThumb.hover.background = thumb;
            skin.horizontalSliderThumb.active.background = thumb;
            skin.horizontalSliderThumb.fixedWidth = 10f;
            skin.horizontalSliderThumb.fixedHeight = 16f;
            return skin;
        }

        /// <summary>Sets GUI.matrix so 1080 units = the screen height. Returns the virtual screen width.</summary>
        public static float Scale()
        {
            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            return Screen.width / s;
        }

        public static void Unscale() => GUI.matrix = Matrix4x4.identity;
    }
}
