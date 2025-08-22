using UnityEngine;
[RequireComponent(typeof(RectTransform))]
public class UIAnimationScale : MonoBehaviour
{
    public bool playOnStart = false;
    public float transitionSpeed = 6f; // higher = snappier
    [Header("Position Settings")]
    public float targetScaleOffset;

    RectTransform rectTransform;
    Vector3 startScale;
    Vector3 endScale;
    bool shouldUpdate = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        startScale = rectTransform.localScale;
        endScale = new Vector2(startScale.x * targetScaleOffset, startScale.y * targetScaleOffset);
    }

    void Start()
    {
        // If the object starts enabled, kick it off here
        if (playOnStart)
            Restart();
    }

    void OnEnable()
    {
        // If the object was disabled at load, OnEnable fires BEFORE Start.
        // We already initialized in Awake, so this is safe.
        if (playOnStart)
            Restart();
    }

    void Update()
    {
        if (!shouldUpdate)
        {
            return;
        }

        if (Vector3.Distance(rectTransform.localScale, endScale) > 0.1f)
        {

            // Exponential smoothing towards target
            float t;
            if (Time.timeScale <= 0)
            {
                t = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);
            }
            else
            {
                t = 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);
            }
            rectTransform.localScale = Vector2.Lerp(rectTransform.localScale, endScale, t);
        }
        else
        {
            rectTransform.localScale = startScale;
            shouldUpdate = false;
        }
    }

    public void Restart()
    {
        shouldUpdate = true;
        rectTransform.localScale = startScale;
    }
}
