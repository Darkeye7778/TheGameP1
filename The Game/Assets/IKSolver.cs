using UnityEngine;
using UnityEngine.Serialization;

public class IKSolver : MonoBehaviour
{
    public Vector3 LookAt;
    public TransformData Grip;
    public float GripWeight = 1.0f;
    [FormerlySerializedAs("LookatWeight")] public float LookAtWeight = 1.0f;

    private Animator _animator;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _animator = GetComponent<Animator>();
    }
    
    private void OnAnimatorIK(int layerIndex)
    {
        _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, GripWeight);
        _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, GripWeight);
        _animator.SetIKPosition(AvatarIKGoal.LeftHand, Grip.Position);
        _animator.SetIKRotation(AvatarIKGoal.LeftHand, Grip.Rotation);
        
        _animator.SetLookAtWeight(LookAtWeight);
        _animator.SetLookAtPosition(LookAt);
    }
}
