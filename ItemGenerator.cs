using UnityEngine;

public class ItemGenerator : MonoBehaviour
{
    [SerializeField]
    private GameObject m_Template;
    [SerializeField]
    private RectTransform m_Rect;

    public GameObject Template => m_Template;
    public RectTransform Rect => m_Rect;

    public GameObject Generate() => Instantiate(m_Template, m_Rect);
    public GameObject Generate(GameObject template, RectTransform rect) => Instantiate(template, rect);
    public GameObject Generate(GameObject template) => Instantiate(template, m_Rect);
    public GameObject Generate(RectTransform rect) => Instantiate(m_Template, rect);
}
