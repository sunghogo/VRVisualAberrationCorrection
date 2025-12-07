using UnityEngine;
using UnityEngine.UI;

public class UIButtonContextTester : MonoBehaviour
{
    [SerializeField] Button targetButton;

    [ContextMenu("Simulate Button Click")]
    void SimulateClick()
    {
        if (targetButton != null)
        {
            targetButton.onClick.Invoke();
        }
        else
        {
            Debug.LogWarning("No target button assigned!");
        }
    }
}
