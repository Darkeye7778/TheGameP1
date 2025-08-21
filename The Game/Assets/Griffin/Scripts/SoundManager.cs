using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class SoundInstance
{ 
    public SoundInstance(Sound sound, GameObject emitter, float radiusMultiplier = 1f)
    {
        Sound = sound;
        Emitter = emitter;
        Position = emitter.transform.position;
        Clip = Sound.PickSound();
        Timer = Sound.Length;
        RadiusMultiplier = radiusMultiplier;
        Layer = emitter.layer;
    }
    
    public SoundInstance(Sound sound, AudioClip clip, GameObject emitter, float radiusMultiplier = 1f)
    {
        Sound = sound;
        Emitter = emitter;
        Position = emitter.transform.position;
        Clip = clip;
        Timer = Sound.Length;
        RadiusMultiplier = radiusMultiplier;
        Layer = emitter.layer;
    }
    
    public SoundInstance(Sound sound, GameObject emitter, int layer, Vector3 position, AudioClip clip, float length, float radiusMultiplier = 1f)
    {
        Sound = sound;
        Emitter = emitter;
        Position = position;
        Clip = clip;
        Timer = length;
        RadiusMultiplier = radiusMultiplier;
        Layer = layer;
    }
    
    
    public AudioClip[] Sounds => Sound.Sounds;
    public float Radius => Sound.Radius * RadiusMultiplier;
    public float Length => Sound.Length;
    public int Priority => Sound.Priority;
    
    public Sound Sound;
    
    public int Layer;
    public GameObject Emitter;
    public Vector3 Position;
    public AudioClip Clip;
    public float Timer;
    public float RadiusMultiplier;
}

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }
    
    [field:SerializeField] public MaterialSettings DefaultSoundProfile { get; private set; }

    private LinkedList<SoundListener> _listeners;
    private LinkedList<SoundInstance> _sounds;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        _listeners = new LinkedList<SoundListener>();
        _sounds = new LinkedList<SoundInstance>();
        Instance = this;
    }

    public void EmitSound(SoundInstance soundInstance) { _sounds.AddLast(soundInstance); }
    public void AddListener(SoundListener listener) { _listeners.AddLast(listener); }
    public void RemoveListener(SoundListener listener) { _listeners.Remove(listener); }

    // Update is called once per frame
    void Update()
    {
        foreach (SoundListener listener in _listeners) 
            listener.ResetSound();
        
        for (LinkedListNode<SoundInstance> node = _sounds.First; node != null;)
        {
            LinkedListNode<SoundInstance> toDelete = node;
            SoundInstance soundInstance = node.Value;
            node = node.Next;
            
            soundInstance.Timer -= Time.deltaTime;
            
            if (soundInstance.Timer <= 0)
            {
                _sounds.Remove(toDelete);
                continue;
            }
            
            foreach (var listener in _listeners)
                listener.ReceiveSound(soundInstance);
        }
    }
}
