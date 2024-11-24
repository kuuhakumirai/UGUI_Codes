using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InfiniteScrollView : ScrollRectComponent
{
    public int Count { get; set; } = 30;
    public int Columns { get; private set; } = 5;
    public int Rows { get; private set; } = 6;

    public Action OnMovingDown;
    public Action OnMovingUp;
    private ScrollRect sr;

    private int targetIndex;
    private float height = 128.0f;
    private bool m_Dragging = false;

    private void Awake()
    {
    }

    private void Update()
    {
    }

    private void Start()
    {
        if (Content.GetComponent<GridLayoutGroup>() is GridLayoutGroup grid)
        {
            if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                Columns = grid.constraintCount;
            }
            height = grid.cellSize.y + grid.spacing.y;
            Rows = (int)(ViewRect.rect.height / height) + 2;
            Content.sizeDelta = new Vector2(Content.sizeDelta.x, Rows * (height));
        }
        targetIndex = Rows - 1;
    }

    protected override void SetContentAnchoredPosition(Vector2 position)
    {
        if (!Horizontal) { position.x = Content.anchoredPosition.x; }
        if (!Vertical) { position.y = Content.anchoredPosition.y; }

        position = AdjustPosition(position); // 计算Content左上角坐标与Viewport左上角坐标的差值，调整Content坐标

        if (position != Content.anchoredPosition)
        {
            Content.anchoredPosition = position;
            UpdateBounds();
        }
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        m_Dragging = true;
        base.OnBeginDrag(eventData);
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        m_Dragging = false;
        base.OnEndDrag(eventData);
    }

    private Vector2 AdjustPosition(Vector2 position)
    {
        switch (targetIndex) // 判断底部行标
        {
            case int when targetIndex >= Count - 1 && Count > Rows:
                if (position.y < 0)
                {
                    MoveDown(ref position);
                }
                break;
            case int when targetIndex > Rows - 1 && Count > Rows:
                if (position.y > height)
                {
                    MoveUp(ref position);
                }
                if (position.y < 0)
                {

                    MoveDown(ref position);
                }
                break;
            case int when targetIndex <= Rows - 1 && Count > Rows:
                if (position.y > height)
                {
                    MoveUp(ref position);
                }
                break;
            default:
                break;
        }
        return position;
    }

    private void MoveUp(ref Vector2 position)
    {
        while (position.y > height) // *
        {
            position.y -= height;
        }
        if (m_Dragging) { m_PointerStartLocalCursor.y += height; } // 如果正在拖拽，同时处理指针起始坐标
        m_PrevPosition.y -= height;
        targetIndex += 1; // 底部行标+1
        OnMovingUp?.Invoke(); // 回调函数处理物体
    }

    private void MoveDown(ref Vector2 position)
    {
        while (position.y < 0)
        {
            position.y += height;
        }
        if (m_Dragging) { m_PointerStartLocalCursor.y -= height; }
        m_PrevPosition.y += height;
        targetIndex -= 1;
        OnMovingDown?.Invoke(); // 回调函数
    }

    public void RefreshView()
    {
        SetContentAnchoredPosition(Vector2.zero);
        targetIndex = Rows - 1;
    }
}

