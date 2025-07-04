using System;
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
    public Mesh Mesh;
    
    public uint Capacity, ReserveCapacity, FireRate;
    public int Damage; // Possible healing weapons?
    public FireMode FireModes;
    public bool OpenBolt;
    public Vector3 Position;

    public float ReloadTime;
    public float EquipTime;
    public float MaxRange;

    public uint FinalCapacity => Capacity + (OpenBolt ? 0u : 1u);
    public float FireDelta => 1.0f / FireRate;

    public FireMode GetDefaultFireMode()
    {
        FireMode mode = FireMode.Auto;
        while ((FireModes & mode) != mode)
            mode = (FireMode)((uint)mode >> 1);
        return mode;
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
    }

    public void Reset()
    {
        LoadedAmmo = Weapon.FinalCapacity;
        ReserveAmmo = Weapon.ReserveCapacity;
        Mode = Weapon.GetDefaultFireMode();
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

        return true;
    }

    public void CycleFireMode()
    {
        do
        {
            // Rotates bits.
            Mode = (FireMode)(((uint) Mode << 1) | (uint) Mode >> (32 - 1));
        } while ((Weapon.FireModes & Mode) != Mode);
    }
}
