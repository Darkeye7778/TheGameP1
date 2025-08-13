using System;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    private enum InventoryState
    {
        Equipping,
        Reloading,
        Ready
    }
    
    [Flags]
    protected enum InputState
    {
        UsePrimary = 1 << 1,
        UseSecondary = 1 << 2,
        Reload = 1 << 3,
        CycleFireMode = 1 << 4,
        Firing = 1 << 5,
        FiringFirst = 1 << 6,
    }
    
    public LayerMask EnemyMask;
    
    [field:SerializeField] public Transform Eye { get; private set; }
    [field:SerializeField] public GameObject Viewmodel { get; private set; }

    [Header("Weapons")] 
    public WeaponInstance Primary;
    public WeaponInstance Secondary;
    
    // TODO: Attach to weapon.
    [Header("Recoil")]
    public CameraRecoil RecoilObj;
    private WeaponMovement _weaponMovement;
    
    public WeaponInstance CurrentWeapon => _useSecondary ? Secondary : Primary;
    public WeaponInstance HolsteredWeapon => _useSecondary ? Primary : Secondary;
    
    public Vector3 UnequippedOffset;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;

    protected InputState InputFlags;
    
    private MeshFilter _viewmodelMesh;
    private Renderer _viewmodelRenderer;
    private bool _useSecondary;
    private float _equipTime;
    private InventoryState _state;
    private PlayerController _player;
    
    protected void Start()
    {
        _viewmodelMesh = Viewmodel.GetComponent<MeshFilter>();
        _viewmodelRenderer = Viewmodel.GetComponent<Renderer>();
        _weaponMovement = Viewmodel.GetComponent<WeaponMovement>();
        if(Primary.Valid) Primary.Reset();
        if(Secondary.Valid) Secondary.Reset();
        
        SetCurrentWeapon(Primary);
    }

    protected void Update()
    {
        Primary.Update();
        Secondary.Update();

        if (InputFlags.HasFlag(InputState.UsePrimary)) InputUsePrimary();
        if (InputFlags.HasFlag(InputState.UseSecondary)) InputUseSecondary();
        
        if(_state != InventoryState.Ready)
        { 
            _equipTime += Time.deltaTime;
            InterpolateWeapon();
            
            if(_state == InventoryState.Reloading && CurrentWeapon.IsEmpty && !_audioSource.isPlaying)
            {
                _audioSource.clip = CurrentWeapon.Weapon.EquipSound;
                _audioSource.Play();
            }

            if (_equipTime < GetInterpolateTime())
                return;

            if(_state == InventoryState.Reloading) 
                CurrentWeapon.Reload();
            
            _state = InventoryState.Ready;
            _equipTime = 0.0f;
        }
        
        if(InputFlags.HasFlag(InputState.CycleFireMode)) 
            InputCycleFireMode();
        if(InputFlags.HasFlag(InputState.Reload)) 
            InputReload();
        if(InputFlags.HasFlag(InputState.Firing))
            InputShoot(InputFlags.HasFlag(InputState.FiringFirst));
        
        Debug.DrawRay(Eye.position, Eye.forward * CurrentWeapon.Weapon.MaxRange, Color.red);
    }
    
    void InputUsePrimary()
    {
        if (CurrentWeapon.Locked || !_useSecondary)
            return;
        
        _useSecondary = false;
        SetCurrentWeapon(CurrentWeapon);
    }

    void InputUseSecondary()
    {
        if (CurrentWeapon.Locked || _useSecondary)
            return;
        
        _useSecondary = true;
        SetCurrentWeapon(CurrentWeapon);
    }

    void InputReload()
    {
        if (CurrentWeapon.Locked || !CurrentWeapon.CanReload)
            return;
        
        _audioSource.clip = CurrentWeapon.Weapon.ReloadSound;
        _audioSource.Play();
        _state = InventoryState.Reloading;
    }

    void InputShoot(bool first)
    {
        if (first && CurrentWeapon.IsEmpty)
        {
            _audioSource.clip = CurrentWeapon.Weapon.EmptySound;
            _audioSource.Play();
        }
            
        switch (CurrentWeapon.Mode)
        {
            case FireMode.Auto:
                    TryShoot();
                break;
            case FireMode.Burst:
                if (first)
                    CurrentWeapon.InitiateBurst();
                if(CurrentWeapon.ShouldBurst)
                    TryShoot();
                break;
            case FireMode.Single:
                if (first)
                    TryShoot();
                break;
        }
    }

    void InputCycleFireMode()
    {
        if (CurrentWeapon.Locked)
            return;
        
        CurrentWeapon.CycleFireMode();
    }

    void ResetCameraRecoil(bool firing, bool first)
    {
        if (CurrentWeapon.Mode == FireMode.Auto)
            RecoilObj.IsShooting = !firing;
        else
            RecoilObj.IsShooting = !firing || !first;
    }

    public void ResetInventory()
    {
        Primary.Reset();
        Secondary.Reset();
        _useSecondary = false;
    }
    
    public void ResetWeapons()
    {
        CurrentWeapon?.Reset();
        HolsteredWeapon?.Reset();
    }
    
    private void SetCurrentWeapon(WeaponInstance weapon)
    {
        _viewmodelMesh.mesh = weapon.Weapon.Mesh;
        _viewmodelRenderer.materials = weapon.Weapon.Materials;
        _viewmodelMesh.transform.localScale = weapon.Weapon.Transform.Scale;
        _viewmodelMesh.transform.localRotation = weapon.Weapon.Transform.Rotation;
        
        _audioSource.clip = weapon.Weapon.EquipSound;
        _audioSource.Play();
        
        _equipTime = 0.0f;
        _state = InventoryState.Equipping;
    }

    private void InterpolateWeapon()
    {
        float fac = _equipTime / GetInterpolateTime();
        Vector3 target = CurrentWeapon.Weapon.Transform.Position;

        Viewmodel.transform.localPosition = Vector3.Slerp(target + UnequippedOffset, target, fac);
    }

    private void TryShoot()
    {
        if (!CurrentWeapon.Shoot())
            return;
        
        RecoilObj.AddRecoil(CurrentWeapon.Weapon);
        if(_weaponMovement != null) 
            _weaponMovement.AddRecoil(CurrentWeapon.Weapon.RecoilIntensity);
        
        _audioSource.clip = CurrentWeapon.Weapon.FireSound;
        _audioSource.PlayOneShot(CurrentWeapon.Weapon.FireSound);
        
        ParticleSystem flash = Instantiate(CurrentWeapon.Weapon.MuzzleFlash,transform.position, Viewmodel.transform.rotation * CurrentWeapon.Weapon.Transform.Rotation);
        flash.transform.parent = Viewmodel.transform;
        flash.transform.localPosition = Quaternion.Inverse(CurrentWeapon.Weapon.Transform.Rotation) * CurrentWeapon.Weapon.Muzzle.Position;
        
        if (!Physics.Raycast(Eye.position, Eye.forward, out RaycastHit hit, CurrentWeapon.Weapon.MaxRange, EnemyMask))
            return;

        if (hit.collider.TryGetComponent(out SoundProfile profile) && profile.GetSettings() != null) 
            Instantiate(profile.GetSettings().HitEffect, hit.point, Quaternion.LookRotation(hit.normal));
        
        if (!hit.collider.TryGetComponent(out IDamagable dmg))
            return;

        dmg.OnTakeDamage(new DamageSource{ Name = name, Object = gameObject }, CurrentWeapon.Weapon.Damage);
    }

    private float GetInterpolateTime()
    {
        return _state switch
        {
            InventoryState.Ready => 0.0f,
            InventoryState.Equipping => CurrentWeapon.Weapon.EquipTime,
            InventoryState.Reloading => CurrentWeapon.Weapon.ReloadTime,
            _ => 0.0f
        };
    }
}
