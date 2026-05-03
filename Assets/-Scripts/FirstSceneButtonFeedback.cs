using UnityEngine;
using UnityEngine.EventSystems;

public class FirstSceneButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RectTransform target;
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float pressedScale = 0.92f;
    [SerializeField] private float bounceScale = 1.1f;
    [SerializeField] private float scaleSpeed = 14f;
    [SerializeField] private float bounceDuration = 0.08f;

    private float desiredScale;
    private float currentVelocity;
    private bool pointerInside;
    private bool pointerDown;
    private float bounceTimer;

    private void Awake()
    {
        if (target == null)
        {
            target = transform as RectTransform;
        }

        desiredScale = normalScale;
        ApplyScaleImmediate(normalScale);
    }

    private void Update()
    {
        if (target == null)
        {
            return;
        }

        if (bounceTimer > 0f)
        {
            bounceTimer -= Time.unscaledDeltaTime;
            if (bounceTimer <= 0f)
            {
                UpdateDesiredScale();
            }
        }

        float nextScale = Mathf.SmoothDamp(target.localScale.x, desiredScale, ref currentVelocity, 1f / scaleSpeed, Mathf.Infinity, Time.unscaledDeltaTime);
        target.localScale = Vector3.one * nextScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerDown = true;
        desiredScale = pressedScale;
        bounceTimer = 0f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerDown = false;
        desiredScale = bounceScale;
        bounceTimer = bounceDuration;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        UpdateDesiredScale();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        pointerDown = false;
        UpdateDesiredScale();
    }

    private void UpdateDesiredScale()
    {
        if (pointerDown)
        {
            desiredScale = pressedScale;
        }
        else if (pointerInside)
        {
            desiredScale = hoverScale;
        }
        else
        {
            desiredScale = normalScale;
        }
    }

    private void ApplyScaleImmediate(float scale)
    {
        if (target != null)
        {
            target.localScale = Vector3.one * scale;
        }
    }
}
