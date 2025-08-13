using UnityEngine;

public class IKSolver : MonoBehaviour
{
    public Vector3 TargetPosition;
    public Quaternion TargetRotation;
    public float Weight = 1.0f;

    private Animator _animator;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _animator = GetComponent<Animator>();
    }
    
    private void OnAnimatorIK(int layerIndex)
    {
        _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, Weight);
        _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, Weight);
        _animator.SetIKPosition(AvatarIKGoal.LeftHand, TargetPosition);
        _animator.SetIKRotation(AvatarIKGoal.LeftHand, TargetRotation);
    }
}
