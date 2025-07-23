using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    private enum InventoryState
    {
        Equipping,
        Reloading,
        Ready
    }
    
    public LayerMask EnemyMask;
    
    [field:SerializeField] public Transform Eye { get; private set; }
    [field:SerializeField] public GameObject Viewmodel { get; private set; }

    [Header("Weapons")] 
    public WeaponInstance Primary;
    public WeaponInstance Secondary;
    
    public WeaponInstance CurrentWeapon => _useSecondary ? Secondary : Primary;
    public WeaponInstance HolsteredWeapon => _useSecondary ? Primary : Secondary;
    
    public Vector3 UnequippedOffset;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    
    private MeshFilter _viewmodelMesh;
    private Renderer _viewmodelRenderer;
    private bool _useSecondary;
    private float _equipTime;
    private InventoryState _state;
    private PlayerController _player;
    private WeaponMovement _weaponMovement;
    
    void Start()
    {
        _viewmodelMesh = Viewmodel.GetComponent<MeshFilter>();
        _viewmodelRenderer = Viewmodel.GetComponent<Renderer>();
        _weaponMovement = Viewmodel.GetComponent<WeaponMovement>();
        if (Primary.Valid) Primary.Reset();
        if(Secondary.Valid) Secondary.Reset();
        
        _player = GameObject.FindWithTag("Player").GetComponent<PlayerController>();
        SetCurrentWeapon(Primary);
    }
    
    void Update()
    {
        if (Time.timeScale == 0.0f)
            return;
        
        Primary.Update();
        Secondary.Update();
        
        if (Input.GetKeyDown(KeyCode.Alpha1) && _useSecondary)
        {
            _useSecondary = false;
            SetCurrentWeapon(CurrentWeapon);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) && !_useSecondary)
        {
            _useSecondary = true;
            SetCurrentWeapon(CurrentWeapon);
        }
        
        if(Input.GetKeyDown(KeyCode.B))
            CurrentWeapon.CycleFireMode();
        
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
        
        Debug.DrawRay(Eye.position, Eye.forward * CurrentWeapon.Weapon.MaxRange, Color.red);

        bool attemptShot = Input.GetButtonDown("Fire1");

        if (attemptShot && CurrentWeapon.IsEmpty)
        {
            _audioSource.clip = CurrentWeapon.Weapon.EmptySound;
            _audioSource.Play();
        }
            
        if (CurrentWeapon.Mode == FireMode.Auto)
        {
            if (Input.GetButton("Fire1"))
                TryShoot();
        } else if (CurrentWeapon.Mode == FireMode.Single)
        {
            if (attemptShot)
                TryShoot();
        }

        if (Input.GetKeyDown(KeyCode.R) && CurrentWeapon.CanReload)
        {
            _audioSource.clip = CurrentWeapon.Weapon.ReloadSound;
            _audioSource.Play();
            _state = InventoryState.Reloading;
        }
    }

    private void SetCurrentWeapon(WeaponInstance weapon)
    {
        _viewmodelMesh.mesh = weapon.Weapon.Mesh;
        _viewmodelRenderer.materials = weapon.Weapon.Materials;
        _viewmodelMesh.transform.localScale = weapon.Weapon.Scale;
        _viewmodelMesh.transform.localRotation = weapon.Weapon.Rotation;
        
        _audioSource.clip = weapon.Weapon.EquipSound;
        _audioSource.Play();
        
        _equipTime = 0.0f;
        _state = InventoryState.Equipping;
    }

    private void InterpolateWeapon()
    {
        float fac = _equipTime / GetInterpolateTime();
        Vector3 target = CurrentWeapon.Weapon.Position;

        Viewmodel.transform.localPosition = Vector3.Slerp(target + UnequippedOffset, target, fac);
    }

    private void TryShoot()
    {
        if (!CurrentWeapon.Shoot())
            return;

        _player.AddRecoil(CurrentWeapon.Weapon.RecoilIntensity);
        _weaponMovement.AddRecoil(CurrentWeapon.Weapon.RecoilIntensity);
        _audioSource.clip = CurrentWeapon.Weapon.FireSound;
        _audioSource.PlayOneShot(CurrentWeapon.Weapon.FireSound);
        
        if (!Physics.Raycast(Eye.position, Eye.forward, out RaycastHit hit, CurrentWeapon.Weapon.MaxRange, EnemyMask))
            return;

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
