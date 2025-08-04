using UnityEngine;

public enum SoundType
{
    Footstep,
    HeavyFootstep,
}

[CreateAssetMenu(fileName = "SoundEmitterSettings", menuName = "Scriptable Objects/SoundEmitterSettings")]
public class SoundEmitterSettings : ScriptableObject
{
    public AudioClip Footstep;
    public AudioClip HeavyFootstep;

    public GameObject HitEffect;

    AudioClip GetSound(SoundType type)
    {
        return type switch
        {
            SoundType.Footstep => Footstep,
            SoundType.HeavyFootstep => HeavyFootstep,
            _ => null
        };
    }
}