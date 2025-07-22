using UnityEngine;

public class DropOff : MonoBehaviour
{
    [SerializeField] private Sprite helicopterSprite;
    [SerializeField] private string noHostagesMessage;
    [SerializeField] private string hostagesCollectedMessage;

    private bool isPlayerInside;

    private void OnTriggerEnter(Collider other)
    {
        if (!isPlayerInside && other.CompareTag("Player"))
        {
            isPlayerInside = true;

            int hostages = gameManager.instance.gameHostageSaved; 

            if (hostages > 0)
            {
                DialogManager.Instance.ShowDialog(
                    helicopterSprite,
                    "Ground Control",
                    hostagesCollectedMessage
                );
            }
            else
            {
                DialogManager.Instance.ShowDialog(
                    helicopterSprite,
                    "Ground Control",
                    noHostagesMessage
                );
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (isPlayerInside && other.CompareTag("Player"))
        {
            isPlayerInside = false;
            DialogManager.Instance.HideDialog();
        }
    }
}