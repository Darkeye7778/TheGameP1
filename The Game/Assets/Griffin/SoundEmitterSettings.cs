using UnityEngine;

public enum SoundType
{
    Footstep,
    HeavyFootstep,
}

[CreateAssetMenu(fileName = "SoundEmitterSettings", menuName = "ScriptableObjects/SoundEmitterSettings")]
public class SoundEmitterSettings : ScriptableObject
{
    public AudioClip Footstep;
    public AudioClip HeavyFootstep;

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