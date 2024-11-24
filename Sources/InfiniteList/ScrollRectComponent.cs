using UnityEngine;
using UnityEngine.EventSystems;

public class ScrollRectComponent : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public enum MovementType
    {
        Unrestricted,
        Elastic,
        Clamped,
    }
    #region membervariables
    [SerializeField]
    private MovementType m_MovementType = MovementType.Elastic;
    public MovementType Movement_Type { get => m_MovementType; set => m_MovementType = value; }
    
    protected RectTransform ViewRect => m_ViewRect;
    public RectTransform m_ViewRect;

    [SerializeField]
    private RectTransform m_Content;
    public RectTransform Content { get => m_Content; set => m_Content = value; }

    [SerializeField]
    private float m_Elasticity = 0.1f;
    public float Elasticity { get => m_Elasticity; set => m_Elasticity = value; }

    [SerializeField]
    private bool m_Inertia = true;
    public bool Inertia { get => m_Inertia; set => m_Inertia = value; }

    private bool m_Horizontal = false;
    public bool Horizontal { get => m_Horizontal; set => m_Horizontal = value; }

    private bool m_Vertical = true;
    public bool Vertical { get => m_Vertical; set => m_Vertical = value; }

    [SerializeField]
    private float m_DecelerationRate = 0.135f; // Only used when inertia is enabled
    public float DecelerationRate { get => m_DecelerationRate; set => m_DecelerationRate = value; }

    private readonly Vector3[] m_Corners = new Vector3[4];

    private bool m_isDragging = false;

    protected Vector2 m_PointerStartLocalCursor = Vector2.zero;
    protected Vector2 m_ContentStartPosition = Vector2.zero;

    protected Bounds m_ContentBounds;
    private Bounds m_ViewBounds;
    public Vector2 Velocity { get => m_Velocity; set => m_Velocity = value; }
    private Vector2 m_Velocity;
   
    protected Vector2 m_PrevPosition = Vector2.zero;
    private Bounds m_PrevContentBounds;
    private Bounds m_PrevViewBounds;
    #endregion
    public virtual void OnBeginDrag(PointerEventData eventData)
    {
        UpdateBounds();

        m_PointerStartLocalCursor = Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(m_ViewRect, eventData.position, eventData.pressEventCamera, out m_PointerStartLocalCursor);
        m_ContentStartPosition = m_Content.anchoredPosition;
        m_isDragging = true;
    }
    public virtual void OnEndDrag(PointerEventData eventData)
    {
        m_isDragging = false;
    }
    public virtual void OnDrag(PointerEventData eventData)
    {
        if (!m_isDragging)  { return; }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_ViewRect, eventData.position, eventData.pressEventCamera, out Vector2 localCursor))
        { return; }

        UpdateBounds();

        var pointerDelta = localCursor - m_PointerStartLocalCursor;
        Vector2 position = m_ContentStartPosition + pointerDelta;

        Vector2 offset = CalculateOffset(position - m_Content.anchoredPosition);
        position += offset;

        if (m_MovementType == MovementType.Elastic)
        {
            if (offset.x != 0)
            {
                position.x -= RubberDelta(offset.x, m_ViewBounds.size.x);  //如果有弹簧效果，则运行其能拉出边界，然后在LateUpdate中进行拉回操作
            }
            if (offset.y != 0)
            {
                position.y -= RubberDelta(offset.y, m_ViewBounds.size.y);
            }
        }
        SetContentAnchoredPosition(position);
    }
    private static float RubberDelta(float overStretching, float viewSize)
    {
        return (1 - (1 / ((Mathf.Abs(overStretching) * 0.55f / viewSize) + 1))) * viewSize * Mathf.Sign(overStretching);
    }
    protected virtual void LateUpdate()
    {
        UpdateBounds();
        float deltaTime = Time.unscaledDeltaTime;
        Vector2 offset = CalculateOffset(Vector2.zero); //是否有拉出边界

        if (deltaTime > 0.0f)
        {
            if (!m_isDragging && (offset != Vector2.zero || m_Velocity != Vector2.zero)) //如果不在拖拽，并且（拉出边界或者速度不为0则进入if）
            {
                Vector2 position = m_Content.anchoredPosition;
                for (int axis = 0; axis < 2; axis++)
                {
                    // Apply spring physics if movement is elastic and content has an offset from the view.
                    if (m_MovementType == MovementType.Elastic && offset[axis] != 0) //超出边界了，则进行拉回
                    {
                        float speed = m_Velocity[axis];
                        position[axis] = Mathf.SmoothDamp(m_Content.anchoredPosition[axis], m_Content.anchoredPosition[axis] + offset[axis], ref speed, m_Elasticity, Mathf.Infinity, deltaTime);
                        if (Mathf.Abs(speed) < 1)
                        {
                            speed = 0;
                        }//如果拉回了，那么speed变为0
                        m_Velocity[axis] = speed;
                    }
                    else if (m_Inertia)
                    {
                        m_Velocity[axis] *= Mathf.Pow(m_DecelerationRate, deltaTime);
                        if (Mathf.Abs(m_Velocity[axis]) < 1)
                        {
                            m_Velocity[axis] = 0;
                        }
                        position[axis] += m_Velocity[axis] * deltaTime;
                    }
                    else
                    {
                        m_Velocity[axis] = 0;
                    }
                }

                if (m_MovementType == MovementType.Clamped) //这里不走，默认为弹簧效果
                {
                    offset = CalculateOffset(position - m_Content.anchoredPosition);
                    position += offset;
                }
                SetContentAnchoredPosition(position); //设置内容区的位置
            }

            if (m_isDragging && m_Inertia) //这里不走，这里没有惯性
            {
                Vector3 newVelocity = (m_Content.anchoredPosition - m_PrevPosition) / deltaTime;
                m_Velocity = Vector3.Lerp(m_Velocity, newVelocity, deltaTime * 10);
            }
        }

        if (m_ViewBounds != m_PrevViewBounds || m_ContentBounds != m_PrevContentBounds || m_Content.anchoredPosition != m_PrevPosition)
        {
            UpdatePrevData(); //更新pre的值
        }
    }
    protected void UpdatePrevData()
    {
        if (m_Content == null)
        {
            m_PrevPosition = Vector2.zero;
        }
        else
            m_PrevPosition = m_Content.anchoredPosition;
        m_PrevViewBounds = m_ViewBounds;
        m_PrevContentBounds = m_ContentBounds;
    }
    protected virtual void SetContentAnchoredPosition(Vector2 position)
    {
        if (!m_Horizontal)
        {
            position.x = m_Content.anchoredPosition.x;
        }
        if (!m_Vertical)
        {
            position.y = m_Content.anchoredPosition.y;
        }

        if (position != m_Content.anchoredPosition)
        {
            m_Content.anchoredPosition = position;
            UpdateBounds();
        }
    }
    private Vector2 CalculateOffset(Vector2 delta)
    {
        return InternalCalculateOffset(ref m_ViewBounds, ref m_ContentBounds, m_Horizontal, m_Vertical, m_MovementType, ref delta);
    }
    internal static Vector2 InternalCalculateOffset(ref Bounds viewBounds, ref Bounds contentBounds, bool horizontal, bool vertical, MovementType movementType, ref Vector2 delta)
    {
        Vector2 offset = Vector2.zero;
        if (movementType == MovementType.Unrestricted)
        {
            return offset;
        }

        Vector2 min = contentBounds.min;
        Vector2 max = contentBounds.max;

        if (horizontal)
        {
            min.x += delta.x;
            max.x += delta.x;

            if (min.x > viewBounds.min.x)
            {
                offset.x = viewBounds.min.x - min.x;
            }
            else if (max.x < viewBounds.max.x)
            {
                offset.x = viewBounds.max.x - max.x;
            }
        }

        if (vertical)
        {
            min.y += delta.y;
            max.y += delta.y;
            if (max.y < viewBounds.max.y)
            {
                offset.y = viewBounds.max.y - max.y;
            }
            else if (min.y > viewBounds.min.y)
            {
                offset.y = viewBounds.min.y - min.y;
            }
        }
        return offset;
    }
    protected void UpdateBounds()
    {
        m_ViewBounds = new Bounds(m_ViewRect.rect.center, m_ViewRect.rect.size);
        m_ContentBounds = GetBounds();
        Vector3 contentSize = m_ContentBounds.size;
        Vector3 contentPos = m_ContentBounds.center;
        var contentPivot = m_Content.pivot;
        AdjustBounds(ref m_ViewBounds, ref contentPivot, ref contentSize, ref contentPos);
        m_ContentBounds.size = contentSize;
        m_ContentBounds.center = contentPos;
    }
    private Bounds GetBounds()
    {
        if (m_Content == null)
        {
            return new Bounds();
        }
        m_Content.GetWorldCorners(m_Corners);
        var viewWorldToLocalMatrix = m_ViewRect.worldToLocalMatrix;
        return InternalGetBounds(m_Corners, ref viewWorldToLocalMatrix);
    }
    internal static Bounds InternalGetBounds(Vector3[] corners, ref Matrix4x4 viewWorldToLocalMatrix)
    {
        var vMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var vMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        for (int j = 0; j < 4; j++)
        {
            Vector3 v = viewWorldToLocalMatrix.MultiplyPoint3x4(corners[j]);
            vMin = Vector3.Min(v, vMin);
            vMax = Vector3.Max(v, vMax);
        }

        var bounds = new Bounds(vMin, Vector3.zero);
        bounds.Encapsulate(vMax);
        return bounds;
    }
    internal static void AdjustBounds(ref Bounds viewBounds, ref Vector2 contentPivot, ref Vector3 contentSize, ref Vector3 contentPos)
    {
        Vector3 excess = viewBounds.size - contentSize;
        if (excess.x > 0)
        {
            contentPos.x -= excess.x * (contentPivot.x - 0.5f);
            contentSize.x = viewBounds.size.x;
        }
        if (excess.y > 0)
        {
            contentPos.y -= excess.y * (contentPivot.y - 0.5f);
            contentSize.y = viewBounds.size.y;
        }
    }
}

