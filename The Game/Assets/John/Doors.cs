using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

public class Doors : MonoBehaviour, Interactable
{
    public Sound sound;
    public float swingAngle = 90f;
    public float openSpeed = 5f;
    private bool _isOpen = false;
    public bool IsOpen => _isOpen;
    private float targetSwing;
    Vector3 origRot;
    Vector3 openRot;
    Vector3 targetRot;

    private AudioSource _audioSource;
    private NavMeshObstacle _navMeshObstacle;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        origRot = transform.eulerAngles;
        openRot = origRot + new Vector3(0, swingAngle, 0);
        _audioSource = GetComponent<AudioSource>();
        _navMeshObstacle = GetComponent<NavMeshObstacle>();
    }

    // Update is called once per frame
    void Update()
    {
        targetRot = _isOpen ? openRot : origRot;
        transform.rotation = Quaternion.Lerp(Quaternion.Euler(transform.eulerAngles), Quaternion.Euler(targetRot), Time.deltaTime * openSpeed);

        if (_navMeshObstacle)
            _navMeshObstacle.carving = _isOpen;
    }
    public void OnInteract(GameObject interactor)
    {
        float newAngle = Vector3.Dot(interactor.transform.forward, transform.forward);
        targetSwing = swingAngle * (newAngle > 0 ? -1 : 1);

        AudioClip clip = sound.PickSound();
        _audioSource.PlayOneShot(clip);
        
        if(interactor.TryGetComponent<PlayerController>(out _)) 
            SoundManager.Instance.EmitSound(new SoundInstance(sound, clip, interactor));
        
        Open();
    }

    public void Open()
    {
        _isOpen = !_isOpen;
        openRot = origRot + new Vector3(0, targetSwing, 0);
    }
}
