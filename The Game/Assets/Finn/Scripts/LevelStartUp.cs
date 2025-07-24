using UnityEngine;

public class LevelStartUp : MonoBehaviour
{
    [SerializeField] Sprite speakerSprite;
    [SerializeField] string speakerName;
    [SerializeField] string message;
    [SerializeField] float delay;

    private void Start()
    {
        Invoke(nameof(TriggerDialog), delay);
    }

    void TriggerDialog()
    {
        DialogManager.Instance.ShowDialog(speakerSprite, speakerName, message);
    }
}
