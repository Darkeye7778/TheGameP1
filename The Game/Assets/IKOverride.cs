using UnityEngine;

public class IKOverride : StateMachineBehaviour
{
    [Range(0, 1)] 
    public float TweenBegin = 0.1f, TweenEnd = 0.9f;

    public float RemapTime = 0.4f;
    
    private IKSolver _solver;
    private float _originalGripWeight, _originalSpeed;
    
    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        _solver = animator.GetComponent<IKSolver>();
        _originalSpeed = animator.speed;
        animator.speed = stateInfo.length / RemapTime;
        if(_solver)
            _originalGripWeight = _solver.LeftGripWeight;
    }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!_solver)
            return;
        
        float time = stateInfo.normalizedTime;
        if (time < TweenBegin)
            _solver.LeftGripWeight = Mathf.Lerp(_originalGripWeight, 0, time / TweenBegin);
        else if (time > TweenEnd)
            _solver.LeftGripWeight = Mathf.Lerp(0, _originalGripWeight, (time - TweenEnd) / (1 - TweenEnd));
        else
            _solver.LeftGripWeight = 0;
    }
    
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if(_solver)
            _solver.LeftGripWeight = _originalGripWeight;
        animator.speed = _originalSpeed;
    }
}
