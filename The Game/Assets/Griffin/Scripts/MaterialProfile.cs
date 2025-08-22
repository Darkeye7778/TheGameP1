using UnityEngine;
using UnityEngine.Serialization;

public class MaterialProfile : MonoBehaviour
{
    [FormerlySerializedAs("_soundProfile")] [SerializeField]
    private MaterialSettings _materialProfile;

    virtual public MaterialSettings GetSettings()
    {
        return _materialProfile; 
    }
}
