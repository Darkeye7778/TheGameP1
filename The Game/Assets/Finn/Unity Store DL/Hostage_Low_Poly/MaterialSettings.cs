using System;
using JetBrains.Annotations;
using UnityEngine;

[Serializable]
public class Sound
{
    public AudioClip[] Sounds = new AudioClip[]{};
    public float Radius = 1;
    public float Length = 0;
    public int Priority = 0;

    [CanBeNull]
    public AudioClip PickSound()
    {
        if (Sounds == null || Sounds.Length == 0)
            return null;

        return Utils.PickRandom(Sounds);
    }
}

public enum SoundType
{
    Footstep,
    HeavyFootstep,
}

[CreateAssetMenu(fileName = "SoundEmitterSettings", menuName = "Scriptable Objects/SoundEmitterSettings")]
public class MaterialSettings : ScriptableObject
{
    public Sound Footstep;
    public Sound HeavyFootstep;

    public GameObject HitEffect;

    Sound GetSound(SoundType type)
    {
        return type switch
        {
            SoundType.Footstep => Footstep,
            SoundType.HeavyFootstep => HeavyFootstep,
            _ => null
        };
    }
}