using UnityEngine;
using System.Collections;
using UnityEngine.AI;
using UnityEngine.ProBuilder.Shapes;

public class enemyAI : MonoBehaviour, IDamagable
{
    [SerializeField] Renderer model;
    [SerializeField] NavMeshAgent agent;
    [SerializeField] Transform shootPos;
    [SerializeField] Transform headPos;
    [SerializeField] Animator anim;

    [SerializeField] float HP;
    [SerializeField] int fov;
    [SerializeField] int faceTargetSpeed;
    [SerializeField] int roamDist;
    [SerializeField] int roamPauseTime;

    [SerializeField] GameObject bullet;
    [SerializeField] float shootRate;

    [SerializeField] GameObject[] Drops;
    [SerializeField] float[] DropWeights;

    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip[] footsteps;
    [SerializeField] AudioClip[] shootSounds;
    bool playingStep;
    public int dropRate;

    Color colorOrg;


    private float _doorCooldown;
    float shootTimer;
    float roamTimer;
    float angleToPlayer;
    float stoppingDistOrig;

    bool playerInTrigger;
    public bool isBomber;

    enum BomberState { Idle, Locking, Charging, Pausing, Reacquire }
    BomberState bomberState = BomberState.Idle;

    Vector3 chargeTarget;
    [SerializeField] float chargeSpeed;
    [SerializeField] float pauseTime;
    [SerializeField] float explosionRange;
    [SerializeField] float explosionDamage;

    Vector3 playerDir;
    Vector3 startingPos;

    // Start is called once before the first Update after the MonoBehavior is created 
    void Start()
    {
        Debug.Log("Start() called on " + gameObject.name);
        Debug.Log("Bullet is: " + bullet);

        colorOrg = model.material.color;
        gameManager.instance.updateTerroristCount(1);
        startingPos = transform.position;
        stoppingDistOrig = agent.stoppingDistance;

        if (bullet == null)
            isBomber = true;
    }
    // Update is called once per frame 
    void Update()
    {
        _doorCooldown += Time.deltaTime;

        if (isBomber)
        {
            BomberUpdate();
        }
        else
        {
            NormalUpdate();
        }

        if (agent.velocity.magnitude > 0.1f && !playingStep)
        {
            StartCoroutine(playFootstep());
        }
        
        if(_doorCooldown > 1)
            CheckDoor();
    }

    void NormalUpdate()
    {
        anim.SetFloat("Speed", agent.velocity.magnitude);

        if (agent.remainingDistance < 0.01f)
            roamTimer += Time.deltaTime;

        if (playerInTrigger && !canSeePlayer())
            roamCheck();
        else if (!playerInTrigger)
            roamCheck();
    }

    void BomberUpdate()
    {
        anim.SetFloat("Speed", agent.velocity.magnitude);

        switch (bomberState)
        {
            case BomberState.Idle:
                if (BomberCanSeePlayer())
                {
                    agent.ResetPath();

                    chargeTarget = gameManager.instance.player.transform.position;
                    chargeTarget.y = transform.position.y;
                    agent.speed = chargeSpeed;
                    agent.stoppingDistance = 0;
                    agent.SetDestination(chargeTarget);
                    bomberState = BomberState.Charging;
                }
                else
                {
                    roamTimer += Time.deltaTime;
                    roamCheck();
                }
                    break;

            case BomberState.Charging:
                if (Vector3.Distance(transform.position, chargeTarget) < 0.5f)
                {
                    bomberState = BomberState.Pausing;
                    StartCoroutine(PauseAndReacquire());
                }
                break;

            case BomberState.Pausing:
                // wait handled in coroutine
                break;

            case BomberState.Reacquire:
                if (BomberCanSeePlayer())
                {
                    chargeTarget = gameManager.instance.player.transform.position;
                    chargeTarget.y = transform.position.y;
                    agent.SetDestination(chargeTarget);
                    bomberState = BomberState.Charging;
                }
                else
                {
                    bomberState = BomberState.Idle;
                }
                break;
        }
    }

    void roamCheck()
    {
        if (roamTimer >= roamPauseTime && agent.remainingDistance < 0.01f)
        {
            roam();
        }
    }
    void roam()
    {
        roamTimer = 0;
        agent.stoppingDistance = 0;

        Vector3 ranPos = Random.insideUnitSphere * roamDist;
        ranPos += startingPos;

        NavMeshHit hit;
        NavMesh.SamplePosition(ranPos, out hit, roamDist, 1);
        agent.SetDestination(hit.position);
    }
    bool canSeePlayer()
    {
        playerDir = gameManager.instance.player.transform.position - headPos.position;
        angleToPlayer = Vector3.Angle(playerDir, transform.forward);

        Debug.DrawRay(headPos.position, playerDir);

        RaycastHit hit;
        if (Physics.Raycast(headPos.position, playerDir, out hit))
        {
            if (hit.collider.CompareTag("Player") && angleToPlayer <= fov)
            {
                shootTimer += Time.deltaTime;

                if (shootTimer > shootRate)
                {
                    shoot();
                }

                agent.SetDestination(gameManager.instance.player.transform.position);

                if (agent.remainingDistance <= agent.stoppingDistance)
                    faceTarget();

                agent.stoppingDistance = stoppingDistOrig;
                return true;
            }
        }
        agent.stoppingDistance = 0;
        return false;
    }
    bool BomberCanSeePlayer()
    {
        Vector3 bomberDir = gameManager.instance.player.transform.position - headPos.position;
        float bomberAngleToPlayer = Vector3.Angle(bomberDir, transform.forward);

        RaycastHit hit;
        if (Physics.Raycast(headPos.position, bomberDir, out hit))
        {
            if (hit.collider.CompareTag("Player") && bomberAngleToPlayer <= fov)
            {
                return true;
            }
        }

        return false;
    }

    void CheckDoor()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, 1f);

        foreach (var collider in colliders)
        {
            Doors door = collider.GetComponent<Doors>();
            if(door != null && !door.isOpen)
            {
                door.OnInteract(gameObject);
                _doorCooldown = 0;
            }
        }
    }

    void faceTarget()
    {
        Quaternion rot = Quaternion.LookRotation(new Vector3(playerDir.x, transform.position.y, playerDir.z));
        transform.rotation = Quaternion.Lerp(transform.rotation, rot, faceTargetSpeed * Time.deltaTime);
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInTrigger = true;

            if (isBomber)
            {
                Explode();
            }
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInTrigger = false;
            agent.stoppingDistance = 0;
        }
    }
    public void OnTakeDamage(DamageSource source, float amount)
    {
        HP -= amount;
        if (HP <= 0)
        {
            gameManager.instance.updateTerroristCount(-1);

            int dropItem = Random.Range(0, 100);
            if (dropItem < dropRate)
            {
                int itemToDrop = GetWeightedDropIndex(DropWeights);
                GameObject drop = Instantiate(Drops[itemToDrop], headPos.position, Quaternion.identity);
                gameManager.instance.RegisterEntity(drop);
            }
            Destroy(gameObject);
        }
        else
        {
            StartCoroutine(flashRed());
            agent.stoppingDistance = stoppingDistOrig;
            agent.SetDestination(source.Object.transform.position);
        }
    }

    public GameObject GameObject()
    {
        return gameObject;
    }

    IEnumerator flashRed()
    {
        model.material.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        model.material.color = colorOrg;
    }

    void shoot()
    {
        shootTimer = 0;

        Instantiate(bullet, shootPos.position, transform.rotation);

        audioSource.clip = shootSounds[Random.Range(0, shootSounds.Length)];
        audioSource.Play();
    }

    int GetWeightedDropIndex(float[] weights)
    {
        float totalWeight = 0f;
        for (int i = 0; i < weights.Length; i++)
            totalWeight += weights[i];

        float randomWeight = Random.Range(0f, totalWeight);

        for (int i = 0; i < weights.Length; i++)
        {
            if (randomWeight < weights[i])
                return i;
            randomWeight -= weights[i];
        }

        return weights.Length - 1; // fallback in case of rounding
    }

    void Explode()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionRange);
        foreach (var hit in hitColliders)
        {
            IDamagable damagable = hit.GetComponent<IDamagable>();
            if (damagable != null && hit.CompareTag("Player"))
            {
                DamageSource dmg = new DamageSource("Bomber", gameObject);
                damagable.OnTakeDamage(dmg, explosionDamage);
            }
        }

        // Instantiate(explosionEffect, transform.position, Quaternion.identity);

        gameManager.instance.updateTerroristCount(-1);
        Destroy(gameObject);
    }


    IEnumerator playFootstep()
    {
        if (playingStep)
            yield break;
        playingStep = true;
        audioSource.clip = footsteps[Random.Range(0, footsteps.Length)];
        audioSource.pitch = Random.Range(0.8f, 1.2f);
        audioSource.Play();
        yield return new WaitForSeconds(audioSource.clip.length);
        playingStep = false;
    }

    IEnumerator PauseAndReacquire()
    {
        yield return new WaitForSeconds(pauseTime);
        bomberState = BomberState.Reacquire;
    }

    public Vector3[] AimTargets()
    {
        throw new System.NotImplementedException();
    }

    public Vector3 LookTarget()
    {
        throw new System.NotImplementedException();
    }

    public bool IsDead()
    {
        return HP > 0;
    }
}
