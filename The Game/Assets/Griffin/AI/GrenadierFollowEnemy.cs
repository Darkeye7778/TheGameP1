using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class GrenadierFollowEnemy : AIState
{
    [field: SerializeField] public Transform Eye { get; private set; }
    [field: SerializeField] public float SuicideDistance { get; set; }
    [field: SerializeField] public Sound SpottedSound { get; private set; }
    [field: SerializeField] public Sound ExplosionSound { get; private set; }
    [field: SerializeField] public AudioSource BombSoundEmitter { get; private set; }

    public float ExplosionDamage;
    public float ExplosionRadius;
    public float ExplosionMinRadius;
    [FormerlySerializedAs("FollowingDistance")] public float StoppingDistance = 2;
    public float FaceSpeed = 5;

    public override bool OverriddenByEnemy()
    {
        return Controller.Target != null;
    }

    public override void OnStart(EnemyAI controller, AIState previousState)
    {
        base.OnStart(controller, previousState);
        
        if(previousState == controller.WanderState)
            Controller.AudioSource.PlayOneShot(SpottedSound.PickSound());

        BombSoundEmitter.pitch = 1.5f;

        Controller.Thoughts.Clear();
    }

    public override void OnUpdate()
    {
        Controller.Agent.stoppingDistance = StoppingDistance;
        
        if(Controller.Target == null)
        {
            Controller.SetState(Controller.WanderState);
            return;
        }
        
        if (Controller.Sight.CanSee())
        {
            Controller.Agent.SetDestination(Controller.Target.GameObject().transform.position);
            
            // Only rotate on z axis.
            Vector3 offset = Controller.Target.AimTarget() - Eye.position;
            offset.y = 0;
            Quaternion targetRotation = Quaternion.LookRotation(offset, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * FaceSpeed);

            bool nearPlayer = Vector3.Distance(transform.position, Controller.Target.GameObject().transform.position) < SuicideDistance;
            if(nearPlayer)
                Explode();
        }
    }

    private void Explode()
    {
        List<IDamagable> inRadius = Explosion.GetObjectsInRadius(transform.position, ExplosionRadius, Controller.EnemyMask);

        foreach (IDamagable dmg in inRadius)
        {
            float distance = Vector3.Distance(dmg.GameObject().transform.position, transform.position);
            float damage = Explosion.Falloff(distance, ExplosionMinRadius, ExplosionRadius) * ExplosionDamage;

            dmg.OnTakeDamage(new DamageSource("Suicide Bomber", gameObject), damage);
        }

        BombSoundEmitter.Pause();
        
        AudioClip clip = ExplosionSound.PickSound();
        AudioSource.PlayClipAtPoint(clip, transform.position);
        SoundManager.Instance.EmitSound(new SoundInstance(ExplosionSound, clip, Controller.Target.GameObject()));

        Controller.OnTakeDamage(new DamageSource(), Controller.Health);
    }

    public override void OnExit(AIState nextState)
    {
        BombSoundEmitter.pitch = 1f;
        if(Controller.IsDead)
            BombSoundEmitter.Pause();
    }

    public override void OnDeath()
    {
        BombSoundEmitter.Pause();
    }
}
