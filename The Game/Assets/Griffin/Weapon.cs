using System;
using System.Linq;
using UnityEngine;

[Flags]
public enum FireMode
{
    Single = 1,
    ThreeRoundBurst = 1 << 1,
    Auto = 1 << 2,
}

[CreateAssetMenu(fileName = "Weapon", menuName = "Scriptable Objects/Weapon")]
public class Weapon : ScriptableObject
{
    public string Name;

    [Header("Display")]
    public Mesh Mesh;
    public Material[] Materials;
    public Vector3 Position;
    public Vector3 Scale;
    public Quaternion Rotation;

    [Header("Sounds")]
    public AudioClip EquipSound;
    public AudioClip FireSound;
    public AudioClip ReloadSound;
    public AudioClip EmptySound;

    [Header("Statistics")]
    public uint Capacity;
    public uint ReserveCapacity;
    public uint FireRate;
    public int Damage; // Possible healing weapons?
    public FireMode FireModes;
    public bool OpenBolt;

    public float ReloadTime;
    public float EquipTime;
    public float MaxRange;
    public float RecoilIntensity;

    public uint FinalCapacity => Capacity + (OpenBolt ? 0u : 1u);
    public float FireDelta => 60.0f / FireRate;

    
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
    public bool HasReserve => ReserveAmmo != 0;
    public bool Valid => Weapon != null;
    public bool CanReload => !IsFull && HasReserve;
    public bool CanShoot => !IsEmpty && _nextShot > Weapon.FireDelta;

    public FireMode Mode;

    private float _nextShot;

    public void Reload()
    {
        if (!CanReload)
            return;

        uint remainingCapacity = Weapon.FinalCapacity - LoadedAmmo;
        uint amountToLoad = Math.Min(ReserveAmmo, remainingCapacity);

        ReserveAmmo -= amountToLoad;
        LoadedAmmo += amountToLoad;
        gameManager.instance.SetAmmoTxt(LoadedAmmo, ReserveAmmo);
    }

    public void Reset()
    {
        LoadedAmmo = Weapon.FinalCapacity;
        ReserveAmmo = Weapon.ReserveCapacity;
        Mode = Weapon.GetDefaultFireMode();
        gameManager.instance.SetAmmoTxt(LoadedAmmo, ReserveAmmo);
        gameManager.instance.SetGunModeText(Mode);
    }

    public void Update()
    {
        _nextShot += Time.deltaTime;
    }

    public bool Shoot()
    {
        if (!CanShoot)
            return false;

        _nextShot = 0.0f;
        --LoadedAmmo;
        gameManager.instance.SetAmmoTxt(LoadedAmmo, ReserveAmmo);
        return true;
    }

    public void CycleFireMode()
    {
        do
        {
            // Rotates bits.
            Mode = (FireMode)(((uint) Mode << 1) | (uint) Mode >> (32 - 1));
        } while ((Weapon.FireModes & Mode) != Mode);
        gameManager.instance.SetGunModeText(Mode);
    }
}
