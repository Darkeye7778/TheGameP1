using System;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public enum InventoryState
    {
        Equipping,
        Reloading,
        Ready
    }
    
    [Flags]
    public enum InputState
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
    [field:SerializeField] public GameObject ViewModel { get; private set; }
    [field:SerializeField] public GameObject WorldModel { get; private set; }
    [field:SerializeField] public Animator Animator { get; private set; }
    [field:SerializeField] public IKSolver IK { get; private set; }

    [Header("Weapons")] 
    public WeaponInstance Primary;
    public WeaponInstance Secondary;
    
    // TODO: Attach to weapon.
    [Header("Recoil")]
    public CameraRecoil RecoilObj;
    private WeaponMovement _weaponMovement;
    
    public WeaponInstance CurrentWeapon => IsUsingPrimary ? Primary : Secondary;
    public WeaponInstance HolsteredWeapon => IsUsingPrimary ? Secondary : Primary;
    
    public Vector3 UnequippedOffset;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;

    protected InputState InputFlags;
    
    private MeshFilter _viewmodelMesh;
    private Renderer _viewmodelRenderer;
    
    private MeshFilter _worldmodelMesh;
    private Renderer _worldmodelRenderer;
    public bool IsUsingPrimary { get; private set; }
    private float _equipTime;
    public InventoryState State { get; private set; }
    private PlayerController _player;
    
    protected void Start()
    {
        if(ViewModel)
        {
            _viewmodelMesh = ViewModel.GetComponent<MeshFilter>();
            _viewmodelRenderer = ViewModel.GetComponent<Renderer>();
            _weaponMovement = ViewModel.GetComponent<WeaponMovement>();
        }

        if (WorldModel)
        {
            _worldmodelMesh = WorldModel.GetComponent<MeshFilter>();
            _worldmodelRenderer = WorldModel.GetComponent<Renderer>();
        }
        else
            IK.LeftGripWeight = IK.RightGripWeight = 0;
        
        if(Primary.Valid) Primary.Reset();
        if(Secondary.Valid) Secondary.Reset();

        IsUsingPrimary = true;
        
        SetCurrentWeapon(Primary);
    }

    protected void Update()
    {
        Primary.Update();
        Secondary.Update();
        
        if(IK && WorldModel)
        {
            IK.RightGrip = TransformData.FromGlobal(WorldModel.transform) * CurrentWeapon.Weapon.Grip;
            IK.LeftGrip = TransformData.FromGlobal(WorldModel.transform) * CurrentWeapon.Weapon.Foregrip;
        }

        if (InputFlags.HasFlag(InputState.UsePrimary)) InputUsePrimary();
        if (InputFlags.HasFlag(InputState.UseSecondary)) InputUseSecondary();
        
        if(State != InventoryState.Ready)
        { 
            _equipTime += Time.deltaTime;
            InterpolateWeapon();
            
            if(State == InventoryState.Reloading && CurrentWeapon.IsEmpty && !_audioSource.isPlaying)
            {
                _audioSource.clip = CurrentWeapon.Weapon.EquipSound;
                _audioSource.Play();
            }

            if (_equipTime < GetInterpolateTime())
                return;

            if(State == InventoryState.Reloading) 
                CurrentWeapon.Reload();
            
            State = InventoryState.Ready;
            _equipTime = 0.0f;
        }
        
        if(InputFlags.HasFlag(InputState.CycleFireMode)) 
            InputCycleFireMode();
        if(InputFlags.HasFlag(InputState.Reload)) 
            InputReload();
        
        InputShoot();
        
        Debug.DrawRay(Eye.position, Eye.forward * CurrentWeapon.Weapon.MaxRange, Color.red);
    }
    
    void InputUsePrimary()
    {
        if (CurrentWeapon.Locked || IsUsingPrimary)
            return;
        
        IsUsingPrimary = true;
        SetCurrentWeapon(CurrentWeapon);
    }

    void InputUseSecondary()
    {
        if (CurrentWeapon.Locked || !IsUsingPrimary)
            return;
        
        IsUsingPrimary = false;
        SetCurrentWeapon(CurrentWeapon);
    }

    void InputReload()
    {
        if (CurrentWeapon.Locked || !CurrentWeapon.CanReload)
            return;
        
        _audioSource.clip = CurrentWeapon.Weapon.ReloadSound;
        _audioSource.Play();
        if(Animator) 
            Animator.SetTrigger("Reload");
        State = InventoryState.Reloading;
    }

    void InputShoot()
    {
        bool shooting = InputFlags.HasFlag(InputState.Firing);
        bool first = InputFlags.HasFlag(InputState.FiringFirst);
        
        if (first && CurrentWeapon.IsEmpty)
        {
            _audioSource.clip = CurrentWeapon.Weapon.EmptySound;
            _audioSource.Play();
        }
            
        switch (CurrentWeapon.Mode)
        {
            case FireMode.Auto:
                    if(shooting) TryShoot();
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
        IsUsingPrimary = true;
    }
    
    public void ResetWeapons()
    {
        CurrentWeapon?.Reset();
        HolsteredWeapon?.Reset();
    }
    
    public void SetCurrentWeapon(WeaponInstance weapon)
    {
        if(ViewModel)
        {
            _viewmodelMesh.sharedMesh = weapon.Weapon.Mesh;
            _viewmodelRenderer.materials = weapon.Weapon.Materials;
            ViewModel.transform.localScale = weapon.Weapon.Transform.Scale;
            ViewModel.transform.localRotation = weapon.Weapon.Transform.Rotation;
        }
        
        if(WorldModel)
        {
            _worldmodelMesh.sharedMesh = weapon.Weapon.Mesh;
            _worldmodelRenderer.materials = weapon.Weapon.Materials;
            WorldModel.transform.localScale = weapon.Weapon.Transform.Scale;
            WorldModel.transform.localRotation = weapon.Weapon.Transform.Rotation;
        }
        
        _audioSource.clip = weapon.Weapon.EquipSound;
        _audioSource.Play();
        
        _equipTime = 0.0f;
        State = InventoryState.Equipping;
    }

    private void InterpolateWeapon()
    {
        float fac = _equipTime / GetInterpolateTime();
        Vector3 target = CurrentWeapon.Weapon.Transform.Position;

        if(ViewModel)
            ViewModel.transform.localPosition = Vector3.Slerp(target + UnequippedOffset, target, fac);
    }

    private void TryShoot()
    {
        if (!CurrentWeapon.Shoot())
            return;
        
        if(RecoilObj)
            RecoilObj.AddRecoil(CurrentWeapon.Weapon);
        if(Animator)
            Animator.SetTrigger("Shoot");
        
        if(_weaponMovement != null) 
            _weaponMovement.AddRecoil(CurrentWeapon.Weapon.RecoilIntensity);
        
        _audioSource.clip = CurrentWeapon.Weapon.FireSound;
        _audioSource.PlayOneShot(CurrentWeapon.Weapon.FireSound);
        
        if(ViewModel)
        {
            ParticleSystem flash = Instantiate(CurrentWeapon.Weapon.MuzzleFlash, transform.position, ViewModel.transform.rotation * CurrentWeapon.Weapon.Transform.Rotation);
            flash.transform.parent = ViewModel.transform;
            flash.transform.localPosition = Quaternion.Inverse(CurrentWeapon.Weapon.Transform.Rotation) * CurrentWeapon.Weapon.Muzzle.Position;
        }
        
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
        return State switch
        {
            InventoryState.Ready => 0.0f,
            InventoryState.Equipping => CurrentWeapon.Weapon.EquipTime,
            InventoryState.Reloading => CurrentWeapon.Weapon.ReloadTime,
            _ => 0.0f
        };
    }
}
