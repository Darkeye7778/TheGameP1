using UnityEngine;
using UnityEngine.Serialization;

public class IKSolver : MonoBehaviour
{
    public Vector3 LookAt;
    
    public TransformData LeftGrip, RightGrip;
    
    [Range(0, 1)] public float LeftGripWeight = 1.0f;
    [Range(0, 1)] public float RightGripWeight = 1.0f;
    [Range(0, 1)] public float LookAtWeight = 1.0f;

    private Animator _animator;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _animator = GetComponent<Animator>();
    }
    
    private void OnAnimatorIK(int layerIndex)
    {
        _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, LeftGripWeight);
        _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, LeftGripWeight);
        _animator.SetIKPosition(AvatarIKGoal.LeftHand, LeftGrip.Position);
        _animator.SetIKRotation(AvatarIKGoal.LeftHand, LeftGrip.Rotation);
        
        _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, RightGripWeight);
        _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, RightGripWeight);
        _animator.SetIKPosition(AvatarIKGoal.RightHand, RightGrip.Position);
        _animator.SetIKRotation(AvatarIKGoal.RightHand, RightGrip.Rotation);
        
        _animator.SetLookAtWeight(LookAtWeight);
        _animator.SetLookAtPosition(LookAt);
    }
}
