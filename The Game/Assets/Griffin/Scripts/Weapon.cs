using System;
using Unity.VisualScripting;
using UnityEngine;

[Flags]
public enum FireMode : uint
{
    Single = 1,
    Burst = 1 << 1,
    Auto = 1 << 2,
    DONOTUSE = 1u << 31
}

[Serializable]
public class TransformData
{
    public Vector3 Position = Vector3.zero;
    public Quaternion Rotation = Quaternion.identity;
    public Vector3 Scale = Vector3.one;

    TransformData(Matrix4x4 transform)
    {
        Position = transform.GetPosition();
        Rotation = transform.rotation;
        Scale = transform.lossyScale;
    }

    TransformData(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }
    
    public static implicit operator Matrix4x4(TransformData t)
    {
        return Matrix4x4.TRS(t.Position, t.Rotation, t.Scale);
    }

    public static TransformData FromLocal(Transform transform)
    {
        return new TransformData
        (
            transform.localPosition,
            transform.localRotation,
            transform.localScale
        );
    }
    
    public static TransformData FromGlobal(Transform transform)
    {
        return new TransformData
        (
            transform.position,
            transform.rotation,
            transform.lossyScale
        );
    }

    public static TransformData operator *(TransformData a, TransformData b)
    {
        Matrix4x4 global = (Matrix4x4) a * (Matrix4x4) b;
        return new TransformData
        (
            global.GetPosition(),
            global.rotation,
            global.lossyScale
        );
    }
}

[CreateAssetMenu(fileName = "Weapon", menuName = "Scriptable Objects/Weapon")]
public class Weapon : ScriptableObject
{
    public string Name;
    public Sprite Image;

    [Header("Display")]
    public Mesh Mesh;
    public Material[] Materials;
    public TransformData Transform;

    public TransformData Grip;
    public TransformData Foregrip;
    public TransformData Muzzle;

    [Header("Sounds")]
    public AudioClip EquipSound;
    public AudioClip FireSound;
    public AudioClip ReloadSound;
    public AudioClip EmptySound;

    [Header("Statistics")]
    public uint Capacity;
    public uint ReserveCapacity;
    
    public uint FireRate;
    public uint BurstFireRate;
    public float BurstCooldown;
    
    public int Damage; // Possible healing weapons?
    public FireMode FireModes; 
    public uint BurstFireCount;
    
    public bool OpenBolt;
    
    public float ReloadTime;
    public float EquipTime;
    public float MaxRange;
    public float RecoilIntensity;
   [Range(0,1)] public float RecoilX;
   [Range(-1,1)] public float RecoilY;

    
    [Header("Effects")]
    public ParticleSystem MuzzleFlash;
    public uint FinalCapacity => Capacity + (OpenBolt ? 0u : 1u);
    public float FireDelta => 60.0f / FireRate;
    public float BurstFireDelta => 60.0f / BurstFireRate;
    
    public FireMode GetDefaultFireMode()
    {
        if (FireModes == 0)
            return 0;

        int shiftCount = sizeof(uint) * 8 - 1;
        for(; shiftCount >= 0; --shiftCount)
            if (((uint)FireModes & 1 << shiftCount) != 0)
                return (FireMode)(1 << shiftCount);

        return 0;
    }
}

[Serializable]
public class WeaponInstance
{
    public Weapon Weapon;
    public uint LoadedAmmo, ReserveAmmo;

    public bool IsEmpty => LoadedAmmo == 0;
    public bool IsFull => LoadedAmmo >= Weapon.FinalCapacity;
    public bool IsLoaded => LoadedAmmo >= Weapon.Capacity;
    public bool HasReserve => ReserveAmmo != 0;
    public bool Valid => Weapon != null;
    public bool CanReload => !IsFull && HasReserve;
    public bool CanShoot => !IsEmpty && _nextShot > _currentFireDelta;
    public bool CanBurst => _remainingBurst == 0 && Mode == FireMode.Burst && CanShoot;
    public bool ShouldBurst => _remainingBurst != 0 && Mode == FireMode.Burst;
    public bool HasAnyAmmo => HasReserve || !IsEmpty;

    public bool Locked { get; private set; }

    public FireMode Mode;

    private float _nextShot;
    private uint _remainingBurst;
    private float _currentFireDelta;

    public void Reload()
    {
        if (!CanReload)
            return;

        uint remainingCapacity = Weapon.FinalCapacity - LoadedAmmo;
        uint amountToLoad = Math.Min(ReserveAmmo, remainingCapacity);

        ReserveAmmo -= amountToLoad;
        LoadedAmmo += amountToLoad;
    }

    public void Reset()
    {
        LoadedAmmo = Weapon.FinalCapacity;
        ReserveAmmo = Weapon.ReserveCapacity;
        Mode = Weapon.GetDefaultFireMode();
        Locked = false;
    }

    public void Update()
    {
        _nextShot += Time.deltaTime;
    }

    public bool Shoot()
    {
        if (!CanShoot)
            return false;

        _currentFireDelta = Weapon.FireDelta;
        if (Mode == FireMode.Burst)
        {
            _currentFireDelta = Weapon.BurstFireDelta;
            _remainingBurst--;
            
            if (_remainingBurst == 0 || LoadedAmmo == 1)
            {
                _currentFireDelta = Weapon.BurstCooldown;
                Locked = false;
            }
        }

        _nextShot = 0.0f;
        --LoadedAmmo;
        
        return true;
    }

    public void InitiateBurst()
    {
        if (!CanBurst)
            return;
        Locked = true;
        _remainingBurst = Weapon.BurstFireCount;
    }

    public void CycleFireMode()
    {
        do
        {
            // Rotates bits.
            Mode = (FireMode)(((uint)Mode << 1) | ((uint)Mode >> 31));
        } while ((Weapon.FireModes & Mode) != Mode);
        
    }
}
