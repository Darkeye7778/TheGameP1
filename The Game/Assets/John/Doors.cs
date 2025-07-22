using UnityEngine;

public class Doors : MonoBehaviour, Interactable
{
    
    public float swingAngle = 90f;
    public float openSpeed = 5f;
    public bool isOpen = false;
    Vector3 origRot;
    Vector3 openRot;
    Vector3 targetRot;




    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        origRot = transform.eulerAngles;
        openRot = origRot + new Vector3(0, swingAngle, 0);
    }

    // Update is called once per frame
    void Update()
    {
        
        targetRot = isOpen ? openRot : origRot;
        transform.rotation = Quaternion.Lerp(Quaternion.Euler(transform.eulerAngles), Quaternion.Euler(targetRot), Time.deltaTime * openSpeed);
    }
    public void OnInteract(GameObject interactor)
    {
        float newAngle = Vector3.Dot(interactor.transform.forward, transform.forward);
        swingAngle *= newAngle > 0 ? -1 : 1;
        Open();
    }

    public void Open()
    {
        isOpen = !isOpen;
        openRot = origRot + new Vector3(0, swingAngle, 0);
    }

}
