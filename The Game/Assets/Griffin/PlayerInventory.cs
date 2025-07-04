using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public LayerMask EnemyMask;
    public Transform Eye;
    public GameObject Viewmodel;
    public WeaponInstance Primary, Secondary;
    public WeaponInstance CurrentWeapon => _useSecondary ? Secondary : Primary;
    public Vector3 UnequippedOffset;
    public bool Equipped => _equipTime >= CurrentWeapon.Weapon.EquipTime;
    
    private MeshFilter _viewmodelRenderer;
    private bool _useSecondary;

    private float _equipTime;
    private float _reloadTime;
    
    void Start()
    {
        _viewmodelRenderer = Viewmodel.GetComponent<MeshFilter>();
        
        if(Primary.Valid) Primary.Reset();
        if(Secondary.Valid) Secondary.Reset();
        
        SetCurrentWeapon(Primary);
    }
    
    void Update()
    {
        Primary.Update();
        Secondary.Update();

        if (_equipTime < CurrentWeapon.Weapon.EquipTime)
            _equipTime += Time.deltaTime;

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
        
        if (!Equipped)
        {
            InterpolateWeapon();
            return;
        }

        if (CurrentWeapon.Mode == FireMode.Auto)
        {
            if (Input.GetButton("Fire1") && CurrentWeapon.Shoot())
                        Shoot();
        } else if (CurrentWeapon.Mode == FireMode.Single)
        {
            if (Input.GetButtonDown("Fire1") && CurrentWeapon.Shoot())
                Shoot();
        }

        if (Input.GetKeyDown(KeyCode.R) && CurrentWeapon.CanReload)
            CurrentWeapon.Reload();
    }

    private void SetCurrentWeapon(WeaponInstance weapon)
    {
        _viewmodelRenderer.mesh = weapon.Weapon.Mesh;
        Viewmodel.transform.localPosition = weapon.Weapon.Position;
        _reloadTime = _equipTime = 0.0f;
    }

    private void InterpolateWeapon()
    {
        float fac = _equipTime / CurrentWeapon.Weapon.EquipTime;
        Vector3 target = CurrentWeapon.Weapon.Position;

        Viewmodel.transform.localPosition = Vector3.Lerp(target + UnequippedOffset, target, fac);
    }

    private void Shoot()
    {
        if (!Physics.Raycast(Eye.position, Eye.forward, out RaycastHit hit, CurrentWeapon.Weapon.MaxRange, EnemyMask))
            return;

        if (!hit.collider.TryGetComponent(out IDamagable dmg))
            return;
                
        dmg.OnTakeDamage(CurrentWeapon.Weapon.Damage);
    }
}
