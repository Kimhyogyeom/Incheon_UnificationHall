using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private SwitchPanelController panel;
    [SerializeField] private float fakeVideoDurationSec = 5f;

    private bool isPlaying;

    void OnEnable()
    {
        if (panel != null) panel.OnButtonPressed += HandleButton;
    }

    void OnDisable()
    {
        if (panel != null) panel.OnButtonPressed -= HandleButton;
    }

    private void HandleButton(int buttonId)
    {
        if (isPlaying)
        {
            Debug.Log($"[Game] Button {buttonId} ignored (playing)");
            return;
        }

        StartCoroutine(PlayVideo(buttonId));
    }

    private IEnumerator PlayVideo(int buttonId)
    {
        isPlaying = true;
        Debug.Log($"[Game] ▶ Play video {buttonId}");
        panel.SetLed(buttonId);

        yield return new WaitForSeconds(fakeVideoDurationSec);

        Debug.Log($"[Game] ■ Video {buttonId} ended");
        panel.ClearLeds();
        isPlaying = false;
    }
}
