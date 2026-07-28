using UnityEngine;

public class RotatingDoor : MonoBehaviour
{
    [Header("旋轉設定")]
    public float openAngle = 90f;
    public float openSpeed = 2f; // 開門速度

    [Header("音效設定 (選填)")]
    public AudioSource audioSource;
    public AudioClip openSound;

    private Quaternion closedRotation; // 原本關門的角度
    private Quaternion targetRotation; // 目標開門的角度
    private bool isOpen = false;

    void Start()
    {
        // 紀錄遊戲開始時門軸的原始角度
        closedRotation = transform.rotation;

        // 計算開門後的目標角度 (繞 Y 軸旋轉)
        targetRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    void Update()
    {
        if (isOpen)
        {
            // 使用 RotateTowards 順暢地旋轉門軸到目標角度
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, openSpeed * 100f * Time.deltaTime);
        }
    }
    // 解謎成功時呼叫這個函數
    public void OpenDoor()
    {
        if (!isOpen)
        {
            isOpen = true;

            // 播放開門音效
            if (audioSource != null && openSound != null)
            {
                audioSource.PlayOneShot(openSound);
            }

            Debug.Log("開門！");
        }
    }
}
