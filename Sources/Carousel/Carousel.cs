using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(ItemGenerator))]
public class Carousel : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    [SerializeField]
    private RectTransform m_Content;

    [Range(1, 9)]
    public int ItemCount = 9;
    [Range(0.0f, 5.0f)]
    public float SizeBase = 1.2f;
    [Range(0, 10)]
    public int Speed = 2;

    private ItemGenerator generator;
    private HorizontalLayoutGroup horizontalLayoutGroup;

    private float width;
    private float delta;

    private Vector2 defaultSize;
    private float itemWidth;
    private float spacing;
    private float contentWidth;
    private Vector3 leftPos;
    private Vector3 rightPos;
    private float e;
    private Vector3 contentStartPos;

    private MyCancellationTokenSource tokenSource;

    private void Awake()
    {
        generator = GetComponent<ItemGenerator>();
        horizontalLayoutGroup = GetComponentInChildren<HorizontalLayoutGroup>();
        width = GetComponent<RectTransform>().rect.width;
        itemWidth = generator.Template.GetComponent<RectTransform>().rect.width;
        contentWidth = m_Content.GetComponent<RectTransform>().rect.width;

        for (int i = 0; i < ItemCount; i++)
        {
            GameObject go = generator.Generate(m_Content);
            go.GetComponentInChildren<Text>().text = i.ToString();
        }
    }

    private void Start()
    {
        if (ItemCount != 1)
        {           
            spacing = (horizontalLayoutGroup.GetComponent<RectTransform>().rect.width - (m_Content.childCount * itemWidth)) / (m_Content.childCount - 1);
        }
        else
        {
            spacing = width;
        }
        horizontalLayoutGroup.spacing = spacing;
        delta = generator.Template.GetComponent<RectTransform>().rect.width + spacing;
        leftPos = m_Content.transform.localPosition - new Vector3(contentWidth / 2 + delta, 0, 0);
        rightPos = m_Content.transform.localPosition + new Vector3(contentWidth / 2 + delta, 0, 0);
        e = (contentWidth / 2 + delta) / (ItemCount / 2);
        defaultSize = generator.Template.GetComponent<RectTransform>().sizeDelta;
        contentStartPos = m_Content.transform.localPosition;

        for (int i = 0; i < m_Content.childCount; i++)
        {
            GameObject go = m_Content.GetChild(i).gameObject;
            float posX = leftPos.x + delta + ((itemWidth + spacing) * i);
            posX += itemWidth / 2;
            float distance = Mathf.Min(Mathf.Abs(posX - leftPos.x), Mathf.Abs(posX - rightPos.x));
            SetSize(go.GetComponent<RectTransform>(), distance);
            SetAlpha(go.GetComponent<RectTransform>(), distance);
            go.SetActive(true);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (tokenSource != null)
        {
            if (!tokenSource.IsDisposed)
            {
                tokenSource.Cancel();
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        MoveCards(eventData.delta.x);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        SetCard();
        tokenSource = new();
        _ = Move();
    }

    private void MoveCards(float pointerOffset)
    {
        pointerOffset = Mathf.Clamp(pointerOffset, -width, width);

        SetCard();

        m_Content.transform.localPosition += new Vector3(pointerOffset, 0, 0);

        if (m_Content.transform.localPosition.x <= -delta)
        {
            m_Content.GetChild(0).SetAsLastSibling();
            m_Content.transform.localPosition = contentStartPos;
        }
        if (m_Content.transform.localPosition.x >= delta)
        {
            m_Content.GetChild(m_Content.childCount - 1).SetAsFirstSibling();
            m_Content.transform.localPosition = contentStartPos;
        }
    }

    private void SetCard()
    {
        for (int i = 0; i < m_Content.childCount; i++)
        {
            GameObject go = m_Content.GetChild(i).gameObject;
            Vector3 pos = go.transform.localPosition + m_Content.localPosition; 
            pos += new Vector3(itemWidth / 2, 0, 0);
            float distance = Mathf.Min(Mathf.Abs((pos - leftPos).x), Mathf.Abs((pos - rightPos).x));
            SetSize(go.GetComponent<RectTransform>(), distance);
            SetAlpha(go.GetComponent<RectTransform>(), distance);
        }
    }

    private void SetSize(RectTransform rect, float distance)
    {
        rect.sizeDelta = defaultSize * new Vector2(1, Mathf.Pow(SizeBase, distance / e));
    }
    private void SetAlpha(RectTransform rect, float distance)
    {
        float val = Mathf.Clamp01(distance / (contentWidth / 3));
        Image[] images = rect.transform.GetComponentsInChildren<Image>();
        for (int i = 0; i < images.Length; i++)
        {
            Color color = images[i].color;
            images[i].color = new(color.r, color.g, color.b, val);
        }
    }

    private async Task Move()
    {
        Vector3 target = Vector3.zero;
        if (m_Content.transform.localPosition.x < -itemWidth / 2)
        {
            target = new Vector3(-delta, 0, 0);
        }
        if (m_Content.transform.localPosition.x > itemWidth / 2)
        {
            target = new Vector3(delta, 0, 0);
        }

        float speed = (m_Content.transform.localPosition - target).magnitude * Speed;
        while (Vector3.Distance(m_Content.transform.localPosition, target) > 0)
        {
            try
            {
                m_Content.transform.localPosition = Vector3.MoveTowards(m_Content.transform.localPosition, target, speed / 100);
                SetCard();
                await Task.Delay((int)(Time.deltaTime * 1000), tokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            finally { }
        }
        tokenSource.Dispose();
    }

    public class MyCancellationTokenSource : CancellationTokenSource
    {
        public bool IsDisposed { get; private set; } = false;
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            IsDisposed = true;
        }
    }
}