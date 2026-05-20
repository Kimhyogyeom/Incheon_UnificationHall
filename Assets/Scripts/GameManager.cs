using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class GameManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SwitchPanelController panel;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private Image fadeOverlay;

    [Header("Clips (Element 0 = 버튼 1)")]
    [SerializeField] private VideoClip[] clips = new VideoClip[6];

    [Header("Fade (sec)")]
    [SerializeField] private float fadeOutDuration = 1.0f; // 검정 → 영상
    [SerializeField] private float fadeInDuration = 1.0f;  // 영상 → 검정

    [Header("Fallback (영상 없을 때 로그용)")]
    [SerializeField] private float fallbackDurationSec = 5f;

    private bool isPlaying;

    void Start()
    {
        SetOverlayAlpha(1f);
    }

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

        VideoClip clip = (buttonId >= 1 && buttonId <= clips.Length) ? clips[buttonId - 1] : null;

        if (clip != null && videoPlayer != null)
        {
            StartCoroutine(PlayWithFade(buttonId, clip));
        }
        else
        {
            Debug.LogWarning($"[Game] No clip for button {buttonId}, fallback timer");
            StartCoroutine(FallbackPlay(buttonId));
        }
    }

    private IEnumerator PlayWithFade(int buttonId, VideoClip clip)
    {
        isPlaying = true;
        Debug.Log($"[Game] ▶ Prepare video {buttonId} ({clip.name})");
        panel.SetLed(buttonId);

        videoPlayer.clip = clip;
        videoPlayer.isLooping = false;
        videoPlayer.playbackSpeed = 1f;

        videoPlayer.Prepare();
        while (!videoPlayer.isPrepared) yield return null;

        videoPlayer.Play();
        Debug.Log($"[Game] ▶ Play {clip.name}");

        yield return Fade(1f, 0f, fadeOutDuration);

        float nextLog = 0f;
        while (videoPlayer.isPlaying)
        {
            if (Time.time >= nextLog)
            {
                Debug.Log($"[Debug] frame={videoPlayer.frame}/{videoPlayer.frameCount} time={videoPlayer.time:F2}/{videoPlayer.length:F2}");
                nextLog = Time.time + 1f;
            }
            yield return null;
        }

        yield return Fade(0f, 1f, fadeInDuration);

        videoPlayer.Stop();
        panel.ClearLeds();
        Debug.Log($"[Game] ■ Video {buttonId} ended");
        isPlaying = false;
    }

    private IEnumerator FallbackPlay(int buttonId)
    {
        isPlaying = true;
        Debug.Log($"[Game] ▶ Fake play video {buttonId}");
        panel.SetLed(buttonId);

        yield return new WaitForSeconds(fallbackDurationSec);

        Debug.Log($"[Game] ■ Fake video {buttonId} ended");
        panel.ClearLeds();
        isPlaying = false;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (fadeOverlay == null || duration <= 0f)
        {
            SetOverlayAlpha(to);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetOverlayAlpha(Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        SetOverlayAlpha(to);
    }

    private void SetOverlayAlpha(float a)
    {
        if (fadeOverlay == null) return;
        Color c = fadeOverlay.color;
        c.a = a;
        fadeOverlay.color = c;
    }
}
