using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogManager : MonoBehaviour
{
    public static DialogManager Instance;

    public GameObject dialogPanel;
    public Image speakerImage;
    public TMP_Text speakerNameText;
    public TMP_Text dialogText;

    private bool dialogActive = false;

    private void Awake()
    {
        Instance = this;
        dialogPanel.SetActive(false);
    }

    private void Update()
    {
        if (dialogActive && Input.GetButtonDown("Interact"))
        {
            HideDialog();
        }
    }

    public void ShowDialog(Sprite speakerSprite, string speakerName, string message)
    {
        speakerImage.sprite = speakerSprite;
        speakerNameText.text = speakerName;
        dialogText.text = message;

        dialogPanel.SetActive(true);
        dialogActive = true;
    }

    public void HideDialog()
    {
        dialogPanel.SetActive(false);
        dialogActive = false;
    }
}
