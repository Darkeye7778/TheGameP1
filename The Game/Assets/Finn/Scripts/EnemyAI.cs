using UnityEngine;
using System.Collections;
using UnityEngine.AI;
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

    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip[] footsteps;
    [SerializeField] AudioClip[] shootSounds;
    bool playingStep;
    public int dropRate;

    Color colorOrg;

  
    float shootTimer;
    float roamTimer;
    float angleToPlayer;
    float stoppingDistOrig;

    bool playerInTrigger;

    Vector3 playerDir;
    Vector3 startingPos;

    // Start is called once before the first Update after the MonoBehavior is created 
    void Start()
    {
        colorOrg = model.material.color;
        gameManager.instance.updateTerroristCount(1);
        startingPos = transform.position;
        stoppingDistOrig = agent.stoppingDistance;
    }
    // Update is called once per frame 
    void Update()
    {
        anim.SetFloat("Speed", agent.velocity.normalized.magnitude);
        if (agent.remainingDistance < 0.01f)
        {
            roamTimer += Time.deltaTime;
        }

        if (playerInTrigger && !canSeePlayer())
        {
            roamCheck();
        }
        else if (!playerInTrigger)
        {
            roamCheck();
        }

        if (agent.velocity.magnitude > 0.1f && !playingStep)
        {
            StartCoroutine(playFootstep());
        }
        CheckDoor();
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

    void CheckDoor()
    {

        RaycastHit hit;
        if (Physics.SphereCast(transform.position, 5f, playerDir, out hit))
        {

            Doors door = hit.collider.GetComponent<Doors>();
            if (door != null)
            {
                if (!door.isOpen)
                {
                    door.OnInteract(gameObject);
                   
                   
                }

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
                int itemToDrop = Random.Range(0, Drops.Length);
                Instantiate(Drops[itemToDrop], headPos.position, Quaternion.identity);
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
}
