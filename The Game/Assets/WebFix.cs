using System;
using UnityEngine;

public class WebFix : MonoBehaviour
{
    private void Start()
    {
        if (Application.platform == RuntimePlatform.WebGLPlayer) Destroy(gameObject);
    }
}
