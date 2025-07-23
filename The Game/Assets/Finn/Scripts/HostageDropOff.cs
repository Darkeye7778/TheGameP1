using UnityEngine;
using System.Collections;

public class HostageDropOff : MonoBehaviour
{
    [SerializeField] private Sprite helicopterSprite;
    [SerializeField] private string noHostagesMessage;
    [SerializeField] private string hostagesCollectedMessage;

    private bool isPlayerInside;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = true;

            int hostages = gameManager.instance.gameHostageSaved;

            if (hostages >= 1)
            {
                DialogManager.Instance.ShowDialog(
                    helicopterSprite,
                    "Ground Control",
                    hostagesCollectedMessage);
                gameManager.instance.updateGameGoal(0 - hostages);
                gameManager.instance.updateHostagesSaved(0 - hostages);
            }
            else
            {
                DialogManager.Instance.ShowDialog(
                    helicopterSprite,
                    "Ground Control",
                    noHostagesMessage);
                Debug.Log("No Hostages, Go Go GO!");
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
