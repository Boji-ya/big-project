using UnityEngine;

public class RotatingDoor : MonoBehaviour
{
    [Header("旋轉設定")]
    public float openAngle = 90f;
    public float openSpeed = 2f;

    [Header("音效設定")]
    public AudioSource audioSource;
    public AudioClip openSound;

    [Header("門的碰撞")]
    public Collider doorCollider;

    private Quaternion closedRotation;
    private Quaternion targetOpenRotation;

    private bool isOpen = false;
    private bool playerInside = false;
    private bool puzzleSolved = false;

    void Start()
    {
        closedRotation = transform.rotation;

        targetOpenRotation =
            closedRotation * Quaternion.Euler(0f, openAngle, 0f);

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    void Update()
    {
        if (isOpen)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetOpenRotation,
                openSpeed * 100f * Time.deltaTime
            );
        }
    }

    // ==========================================
    // 玩家在外面比讚 → 開門
    // ==========================================
    public void OpenFromGesture()
    {
        // 進入冷凍庫後，就不能再用比讚開門
        if (playerInside)
        {
            Debug.Log("玩家已經在冷凍庫內，比讚不能開門！");
            return;
        }

        // 已經解謎成功就不用再開
        if (puzzleSolved)
            return;

        OpenDoor();
    }

    // ==========================================
    // 真正的開門
    // 解謎成功使用
    // ==========================================
    public void OpenDoor()
    {
        if (!isOpen)
        {
            isOpen = true;

            // 開門時取消門的碰撞
            if (doorCollider != null)
            {
                doorCollider.enabled = false;
            }

            if (audioSource != null && openSound != null)
            {
                audioSource.PlayOneShot(openSound);
            }

            Debug.Log("開門！");
        }
    }

    // ==========================================
    // 玩家進入冷凍庫
    // ==========================================
    public void PlayerEnterFreezer()
    {
        if (playerInside)
            return;

        playerInside = true;

        Debug.Log("玩家進入冷凍庫，關門並鎖住！");

        CloseDoor();
    }

    // ==========================================
    // 關門
    // ==========================================
    public void CloseDoor()
    {
        isOpen = false;

        // 直接回到關門角度
        transform.rotation = closedRotation;

        // 開啟碰撞，玩家無法出去
        if (doorCollider != null)
        {
            doorCollider.enabled = true;
        }

        Debug.Log("門已關閉！");
    }

    // ==========================================
    // 拉桿解謎成功
    // ==========================================
    public void PuzzleComplete()
    {
        puzzleSolved = true;

        Debug.Log("解謎成功，冷凍庫門解鎖！");

        // 關閉門的碰撞
        if (doorCollider != null)
        {
            doorCollider.enabled = false;
        }

        OpenDoor();
    }

    // ==========================================
    // 玩家離開冷凍庫
    // ==========================================
    public void PlayerExitFreezer()
    {
        playerInside = false;

        Debug.Log("玩家離開冷凍庫！");
    }

    public bool IsPuzzleSolved()
    {
        return puzzleSolved;
    }
}