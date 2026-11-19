using UnityEngine;

namespace Perun.Client.UI;

internal sealed class VirtualList
{
    private const int Margin = 2;

    private float _viewHeight = 400f;
    private int _count;

    public VirtualList(float rowHeight)
    {
        RowHeight = rowHeight;
    }

    public float RowHeight { get; }
    public int First { get; private set; }
    public int Last { get; private set; }

    public void Plan(int count, Vector2 scroll)
    {
        if (Event.current.type != EventType.Layout)
        {
            return;
        }

        _count = count;
        int visible = Mathf.CeilToInt(_viewHeight / RowHeight) + Margin * 2;
        First = Mathf.Clamp(Mathf.FloorToInt(scroll.y / RowHeight) - Margin, 0, count);
        Last = Mathf.Min(count, First + visible);
    }

    public void SpaceBefore()
    {
        if (First > 0)
        {
            GUILayout.Space(First * RowHeight);
        }
    }

    public void SpaceAfter()
    {
        int rest = _count - Last;
        if (rest > 0)
        {
            GUILayout.Space(rest * RowHeight);
        }
    }

    public void MeasureView()
    {
        if (Event.current.type == EventType.Repaint)
        {
            float height = GUILayoutUtility.GetLastRect().height;
            if (height > 1f)
            {
                _viewHeight = height;
            }
        }
    }
}
