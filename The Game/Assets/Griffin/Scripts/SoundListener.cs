using System;
using UnityEngine;

public class SoundListener : MonoBehaviour
{
    [field: SerializeField] public LayerMask Mask { get; private set; }
    [field: SerializeField] public SoundInstance CurrentSoundInstance { get; private set; }
    public float CurrentSoundDistance { get; private set; }
    [field: SerializeField] public float HearingStrength { get; private set; } = 1;

    public bool SoundChanged => _previousSoundInstance != CurrentSoundInstance && CurrentSoundInstance != null;

    private SoundInstance _previousSoundInstance;

    public void ReceiveSound(SoundInstance soundInstance)
    {
        if (Mask != 0 && ((1 << soundInstance.Layer) & Mask) == 0)
            return;
        
        float distance = Vector3.Distance(soundInstance.Position, transform.position);
        if (distance > soundInstance.Radius * HearingStrength)
            return;
        
        if(CurrentSoundInstance != null)
        {
            if (soundInstance.Priority <= CurrentSoundInstance.Priority && distance >= CurrentSoundDistance)
                return;
        }
        
        CurrentSoundInstance = soundInstance;
        CurrentSoundDistance = distance;
    }

    public void ResetSound()
    {
        _previousSoundInstance = CurrentSoundInstance;
        CurrentSoundInstance = null;
        CurrentSoundDistance = 0;
    }
    
    void Start()
    {
        CurrentSoundInstance = null;
        _previousSoundInstance = null;
        
        SoundManager.Instance.AddListener(this);
    }

    private void OnDestroy()
    {
        SoundManager.Instance.RemoveListener(this);
    }
}
