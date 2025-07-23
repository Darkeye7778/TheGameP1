using Unity.VisualScripting;
using UnityEngine;

public class SoundProfile : MonoBehaviour
{
    [SerializeField]
    private SoundEmitterSettings _soundProfile;

    public SoundEmitterSettings GetSettings()
    {
        return _soundProfile; 
    }
}
