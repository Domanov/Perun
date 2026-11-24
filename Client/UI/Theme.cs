using UnityEngine;

namespace Perun.Client.UI;

internal static class Theme
{
    public static readonly Color Accent = new(1f, 0.78f, 0.28f);
    public static readonly Color Text = new(0.9f, 0.92f, 0.94f);
    public static readonly Color Dim = new(0.6f, 0.66f, 0.72f);
    public static readonly Color Danger = new(1f, 0.42f, 0.38f);

    public static GUIStyle Panel { get; private set; }
    public static GUIStyle Header { get; private set; }
    public static GUIStyle Title { get; private set; }
    public static GUIStyle Tab { get; private set; }
    public static GUIStyle Button { get; private set; }
    public static GUIStyle DangerButton { get; private set; }
    public static GUIStyle Row { get; private set; }
    public static GUIStyle RowSelected { get; private set; }
    public static GUIStyle RowBadged { get; private set; }
    public static GUIStyle RowSelectedBadged { get; private set; }
    public static GUIStyle SectionRow { get; private set; }
    public static GUIStyle SectionRowSelected { get; private set; }
    public static GUIStyle Badge { get; private set; }
    public static GUIStyle Group { get; private set; }
    public static GUIStyle Label { get; private set; }
    public static GUIStyle Small { get; private set; }
    public static GUIStyle Search { get; private set; }
    public static GUIStyle Bar { get; private set; }
    public static GUIStyle BarText { get; private set; }
    public static GUIStyle BarTextRight { get; private set; }
    public static GUIStyle Toast { get; private set; }

    private static Texture2D _panel;
    private static readonly string[] Percents = new string[101];
    private static readonly string[] Degrees = new string[201];
    private static readonly string[] Clock = new string[24 * 60];
    private static readonly string[] Factors = new string[121];

    public static string Percent(int value)
    {
        value = Mathf.Clamp(value, 0, 100);
        return Percents[value] ??= value + "%";
    }

    public static string Celsius(float value)
    {
        int index = Mathf.Clamp(Mathf.RoundToInt(value), -100, 100) + 100;
        return Degrees[index] ??= (index - 100) + " °C";
    }

    public static string Time(float hours)
    {
        int minutes = Mathf.Clamp(Mathf.RoundToInt(hours * 60f), 0, Clock.Length - 1);
        return Clock[minutes] ??= $"{minutes / 60:00}:{minutes % 60:00}";
    }

    public static string Time(System.DateTime time) => Time(time.Hour + time.Minute / 60f);

    public static string Factor(float factor)
    {
        int index = Mathf.RoundToInt(factor * 2f);
        if (index < 0 || index >= Factors.Length)
        {
            return $"×{factor:0.#}";
        }
        return Factors[index] ??= $"×{index / 2f:0.#}";
    }

    public static void Ensure()
    {
        if (Panel != null && _panel != null)
        {
            return;
        }

        _panel = Fill(new Color(0.07f, 0.08f, 0.09f, 0.9f));
        Texture2D header = Fill(new Color(0.13f, 0.15f, 0.17f, 0.95f));
        Texture2D button = Fill(new Color(0.17f, 0.19f, 0.21f, 1f));
        Texture2D hover = Fill(new Color(0.24f, 0.27f, 0.3f, 1f));
        Texture2D accent = Fill(new Color(Accent.r, Accent.g, Accent.b, 0.9f));
        Texture2D accentDim = Fill(new Color(Accent.r, Accent.g, Accent.b, 0.28f));
        Texture2D rowHover = Fill(new Color(1f, 1f, 1f, 0.07f));
        Texture2D danger = Fill(new Color(0.45f, 0.12f, 0.1f, 1f));
        Texture2D dangerHover = Fill(new Color(0.62f, 0.16f, 0.13f, 1f));
        Texture2D field = Fill(new Color(0.03f, 0.035f, 0.04f, 1f));

        Panel = new GUIStyle { padding = new RectOffset(8, 8, 8, 8) };
        Panel.normal.background = _panel;

        Header = new GUIStyle
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(8, 8, 4, 4),
            richText = true
        };
        Header.normal.background = header;
        Header.normal.textColor = Accent;

        Title = new GUIStyle
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            richText = true
        };
        Title.normal.textColor = Accent;

        Tab = Clickable(button, hover, accent, 12, FontStyle.Bold);
        Tab.onNormal.textColor = Color.black;
        Tab.onHover.textColor = Color.black;
        Tab.alignment = TextAnchor.MiddleCenter;
        Tab.margin = new RectOffset(1, 1, 1, 1);

        Button = Clickable(button, hover, accent, 12, FontStyle.Normal);
        Button.alignment = TextAnchor.MiddleCenter;
        Button.margin = new RectOffset(2, 2, 2, 2);

        DangerButton = Clickable(danger, dangerHover, dangerHover, 12, FontStyle.Normal);
        DangerButton.alignment = TextAnchor.MiddleCenter;
        DangerButton.margin = new RectOffset(2, 2, 2, 2);

        Row = new GUIStyle
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(10, 6, 3, 3),
            richText = true,
            clipping = TextClipping.Clip
        };
        Row.normal.textColor = Text;
        Row.hover.background = rowHover;
        Row.hover.textColor = Color.white;

        RowSelected = new GUIStyle(Row);
        RowSelected.normal.background = accentDim;
        RowSelected.hover.background = accentDim;
        RowSelected.normal.textColor = Color.white;

        RowBadged = new GUIStyle(Row) { padding = new RectOffset(10, 76, 3, 3) };
        RowSelectedBadged = new GUIStyle(RowSelected) { padding = new RectOffset(10, 76, 3, 3) };
        SectionRow = new GUIStyle(Row) { padding = new RectOffset(22, 76, 3, 3) };
        SectionRowSelected = new GUIStyle(RowSelected) { padding = new RectOffset(22, 76, 3, 3) };
        Badge = new GUIStyle { fontSize = 11, richText = true, alignment = TextAnchor.MiddleRight, padding = new RectOffset(0, 8, 0, 0) };

        Group = new GUIStyle(Row) { fontStyle = FontStyle.Bold, padding = new RectOffset(4, 6, 4, 4) };
        Group.normal.textColor = Dim;
        Group.hover.background = rowHover;

        Label = new GUIStyle { fontSize = 12, richText = true, wordWrap = true, padding = new RectOffset(2, 2, 2, 2) };
        Label.normal.textColor = Text;

        Small = new GUIStyle(Label) { fontSize = 11 };
        Small.normal.textColor = Dim;

        Search = new GUIStyle
        {
            fontSize = 12,
            padding = new RectOffset(6, 6, 4, 4),
            margin = new RectOffset(2, 2, 2, 4),
            clipping = TextClipping.Clip
        };
        Search.normal.background = field;
        Search.focused.background = field;
        Search.normal.textColor = Text;
        Search.focused.textColor = Color.white;

        Bar = new GUIStyle { fontSize = 12, richText = true, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 0, 0) };
        Bar.normal.background = _panel;
        Bar.normal.textColor = Text;

        BarText = new GUIStyle(Bar);
        BarText.normal.background = null;
        BarTextRight = new GUIStyle(BarText) { alignment = TextAnchor.MiddleRight };

        Toast = new GUIStyle(Bar) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
        Toast.normal.background = header;
    }

    private static GUIStyle Clickable(Texture2D normal, Texture2D hover, Texture2D on, int size, FontStyle font)
    {
        GUIStyle style = new()
        {
            fontSize = size,
            fontStyle = font,
            padding = new RectOffset(8, 8, 5, 5),
            richText = true
        };
        style.normal.background = normal;
        style.normal.textColor = Text;
        style.hover.background = hover;
        style.hover.textColor = Color.white;
        style.active.background = on;
        style.active.textColor = Color.black;
        style.onNormal.background = on;
        style.onHover.background = on;
        style.onActive.background = on;
        style.onNormal.textColor = Color.black;
        return style;
    }

    private static Texture2D Fill(Color color)
    {
        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    public static void Rect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
        GUI.color = previous;
    }

    public static string PrettyRole(string name)
    {
        System.Text.StringBuilder b = new(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i == 0)
            {
                b.Append(char.ToUpperInvariant(c));
                continue;
            }
            if (char.IsUpper(c) && !char.IsUpper(name[i - 1]))
            {
                b.Append(' ');
            }
            b.Append(c);
        }
        return b.ToString();
    }
}
