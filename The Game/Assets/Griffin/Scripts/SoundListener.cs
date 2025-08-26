using System;
using UnityEngine;

public class SoundListener : MonoBehaviour
{
    [field: SerializeField] public LayerMask Mask { get; private set; }

    public SoundInstance CurrentSoundInstance => _currentSoundInstance;
    
    private SoundInstance _currentSoundInstance;
    public float CurrentSoundDistance { get; private set; }
    [field: SerializeField] public float HearingStrength { get; private set; } = 1;

    public bool SoundChanged => _previousSoundInstance != _currentSoundInstance && _currentSoundInstance != null;

    private SoundInstance _previousSoundInstance;

    public void ReceiveSound(SoundInstance soundInstance)
    {
        if (soundInstance == null)
            return;
        
        if (Mask != 0 && ((1 << soundInstance.Layer) & Mask) == 0)
            return;
        
        float distance = Vector3.Distance(soundInstance.Position, transform.position);
        if (distance > soundInstance.Radius * HearingStrength)
            return;
        
        if(_currentSoundInstance != null)
        {
            if (soundInstance.Priority <= _currentSoundInstance.Priority && distance >= CurrentSoundDistance)
                return;
        }
        
        _currentSoundInstance = soundInstance;
        CurrentSoundDistance = distance;
    }

    public void ResetSound()
    {
        _previousSoundInstance = _currentSoundInstance;
        _currentSoundInstance = null;
        CurrentSoundDistance = 0;
    }
    
    void Start()
    {
        _currentSoundInstance = null;
        _previousSoundInstance = null;
        
        SoundManager.Instance.AddListener(this);
    }

    private void OnDestroy()
    {
        SoundManager.Instance.RemoveListener(this);
    }
}
