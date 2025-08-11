using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System.Collections;
using Unity.VisualScripting;

public class UIAnimation : MonoBehaviour
{
    public float transitionSpeed = 0.2f;
    
    [Header("Position Settings")]
    public Vector2 startingPosOffset;
    RectTransform rectTransform;

    

    Vector2 startPos;
    Vector2 endPos;
    
    

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        
        endPos = rectTransform.anchoredPosition;
        startPos = endPos + startingPosOffset;

        Invoke(nameof(Restart), 0.05f);
    }
    
    
    IEnumerator Interpolate()
    {
        while (Vector2.Distance(rectTransform.anchoredPosition, endPos) > 0.01f)
        {
            
            // Position interpolation
            rectTransform.anchoredPosition = Vector3.Lerp(rectTransform.anchoredPosition, endPos, transitionSpeed * Time.fixedUnscaledDeltaTime);

            yield return null;
        }
        
        rectTransform.anchoredPosition = endPos;
    }


    private void OnEnable()
    {
        Restart();
    }

    void Restart()
    {
        if (rectTransform == null) return;
        rectTransform.anchoredPosition = startPos;
        StartCoroutine(Interpolate());
    }
    
}
    

