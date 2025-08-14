using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System.Collections;
using Unity.VisualScripting;

[RequireComponent(typeof(RectTransform))]
public class UIAnimation : MonoBehaviour
{
    public float transitionSpeed = 6f; // higher = snappier
    [Header("Position Settings")]
    public Vector2 startingPosOffset;

    RectTransform rectTransform;
    Vector2 startPos;
    Vector2 endPos;
    bool shouldUpdate;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        endPos = rectTransform.anchoredPosition;
        startPos = endPos + startingPosOffset;
    }

    void Start()
    {
        // If the object starts enabled, kick it off here
        Restart();
    }

    void OnEnable()
    {
        // If the object was disabled at load, OnEnable fires BEFORE Start.
        // We already initialized in Awake, so this is safe.
        Restart();
    }

    void Update()
    {
        if (!shouldUpdate) return;

        if (Vector2.Distance(rectTransform.anchoredPosition, endPos) > 0.01f)
        {
            // Exponential smoothing towards target
            float t = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);
            rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, endPos, t);
        }
        else
        {
            rectTransform.anchoredPosition = endPos;
            shouldUpdate = false;
        }
    }

    public void Restart()
    {
        shouldUpdate = true;
        rectTransform.anchoredPosition = startPos;
    }
}
    

