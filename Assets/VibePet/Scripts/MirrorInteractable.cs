using UnityEngine;

// 場景中的鏡子互動：被點擊時開啟換裝面板
public class MirrorInteractable : MonoBehaviour
{
    [Header("UI Panel")]
    public GameObject dressUpPanelRoot; // 指向 MirrorDressUpPanel 所在的面板根節點

    [Tooltip("開啟面板時，是否鎖住時間")]
    public bool pauseTimeOnOpen = false;

    private void OnMouseDown()
    {
        if (dressUpPanelRoot)
        {
            dressUpPanelRoot.SetActive(true);
            if (pauseTimeOnOpen)
            {
                Time.timeScale = 0f;
            }
        }
    }
}


