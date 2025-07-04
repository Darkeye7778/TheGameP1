using Unity.VisualScripting;
using UnityEngine;

public class SoundEmitter : MonoBehaviour
{
    [SerializeField]
    private SoundEmitterSettings _soundProfile;

    public SoundEmitterSettings GetSettings()
    {
        return _soundProfile; 
    }
}
