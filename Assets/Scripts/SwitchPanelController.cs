using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class SwitchPanelController : MonoBehaviour
{
    [Header("Serial")]
    [SerializeField] private string portName = "COM3";
    [SerializeField] private int baudRate = 9600;
    [SerializeField] private int readTimeoutMs = 100;

    [Header("Button Remap (raw panel ID → logical ID)")]
    [Tooltip("index 0 = 판넬이 보내는 1번, 값 = 코드에서 쓸 논리 번호. 예: {3,2,1,6,5,4}")]
    [SerializeField] private int[] buttonRemap = { 1, 2, 3, 4, 5, 6 };

    [Header("LED Output")]
    [Tooltip("LED 명령에도 0xFA 헤더를 같이 보낼지. 종이 스펙 확인 후 켜고 끄세요.")]
    [SerializeField] private bool useHeaderForLed = false;

    private const byte HeaderByte = 0xFA;
    private const byte LedAllOff = 0x30;
    private const int BuggyRawLed = 1; // 펌웨어 버그: raw 1번은 0x30으로 안 꺼짐
    private const byte WorkaroundFlashRaw = 0x32; // 우회용 더미 LED (raw 2)

    public event Action<int> OnButtonPressed;

    private SerialPort port;
    private Thread readThread;
    private volatile bool running;
    private readonly ConcurrentQueue<int> pendingButtons = new ConcurrentQueue<int>();
    private int lastSetRawLed = 0;

    void Start()
    {
        try
        {
            port = new SerialPort(portName, baudRate)
            {
                ReadTimeout = readTimeoutMs,
                WriteTimeout = 200,
            };
            port.Open();
            Debug.Log($"[Panel] Opened {portName} @ {baudRate}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Panel] Failed to open {portName}: {e.Message}. Running without panel.");
            port = null;
            return;
        }

        ClearLeds();

        running = true;
        readThread = new Thread(ReadLoop) { IsBackground = true, Name = "PanelReader" };
        readThread.Start();
    }

    void Update()
    {
        while (pendingButtons.TryDequeue(out int buttonId))
        {
            OnButtonPressed?.Invoke(buttonId);
        }
    }

    void OnApplicationQuit()
    {
        Shutdown();
    }

    void OnDestroy()
    {
        Shutdown();
    }

    public void SetLed(int logicalId)
    {
        int rawId = LogicalToRaw(logicalId);
        if (rawId < 1 || rawId > 6) return;
        byte cmd = (byte)(0x30 + rawId);
        byte[] payload = useHeaderForLed ? new[] { HeaderByte, cmd } : new[] { cmd };
        WriteBytes(payload, useHeaderForLed ? $"0xFA 0x{cmd:X2}" : $"0x{cmd:X2}");
        lastSetRawLed = rawId;
    }

    public void ClearLeds()
    {
        // 펌웨어 버그 우회: raw LED 1번이 마지막으로 켜져 있었으면
        // 0x30이 안 먹으니, 다른 LED로 단독점등 한 번 거쳐서 LED 1을 꺼뜨린 뒤 전체끄기.
        if (lastSetRawLed == BuggyRawLed)
        {
            byte[] flash = useHeaderForLed
                ? new[] { HeaderByte, WorkaroundFlashRaw }
                : new[] { WorkaroundFlashRaw };
            WriteBytes(flash, useHeaderForLed ? $"0xFA 0x{WorkaroundFlashRaw:X2} (workaround)" : $"0x{WorkaroundFlashRaw:X2} (workaround)");
        }

        byte[] payload = useHeaderForLed ? new[] { HeaderByte, LedAllOff } : new[] { LedAllOff };
        string repr = useHeaderForLed ? $"0xFA 0x{LedAllOff:X2}" : $"0x{LedAllOff:X2}";
        WriteBytes(payload, repr);
        lastSetRawLed = 0;
    }

    private void WriteBytes(byte[] payload, string logRepr)
    {
        if (port == null || !port.IsOpen) return;
        try
        {
            port.Write(payload, 0, payload.Length);
            Debug.Log($"[Panel] LED → {logRepr}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Panel] Write failed: {e.Message}");
        }
    }

    private int RawToLogical(int raw)
    {
        if (raw < 1 || raw > buttonRemap.Length) return raw;
        return buttonRemap[raw - 1];
    }

    private int LogicalToRaw(int logical)
    {
        for (int i = 0; i < buttonRemap.Length; i++)
        {
            if (buttonRemap[i] == logical) return i + 1;
        }
        return logical;
    }

    private void ReadLoop()
    {
        while (running)
        {
            try
            {
                int b = port.ReadByte();
                if (b != HeaderByte) continue;

                int data = port.ReadByte();
                if (data >= 0x31 && data <= 0x36)
                {
                    int rawId = data - 0x30;
                    pendingButtons.Enqueue(RawToLogical(rawId));
                }
            }
            catch (TimeoutException) { }
            catch (Exception e)
            {
                if (running) Debug.LogWarning($"[Panel] Read error: {e.Message}");
                break;
            }
        }
    }

    private void Shutdown()
    {
        running = false;
        try { readThread?.Join(500); } catch { }

        if (port != null && port.IsOpen)
        {
            try { ClearLeds(); } catch { }
            try { port.Close(); } catch { }
        }
        port = null;
    }
}
