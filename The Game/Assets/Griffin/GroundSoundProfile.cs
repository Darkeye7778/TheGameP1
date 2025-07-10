using Unity.VisualScripting;
using UnityEngine;

public class GroundSoundProfile : MonoBehaviour
{
    [SerializeField]
    private SoundEmitterSettings _soundProfile;

    public SoundEmitterSettings GetSettings()
    {
        return _soundProfile; 
    }
}
