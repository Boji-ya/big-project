using System.Collections;
using UnityEngine;
using TMPro;

public class WhackAMoleManager : MonoBehaviour
{
    [Header("5 隻火雞地鼠陣列")]
    public TurkeyMole[] turkeyMoles;

    [Header("機台看板 UI (World Space Text)")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;

    // ============================================
    // 音效設定
    // ============================================
    [Header("音效設定")]
    public AudioSource audioSource;
    public AudioClip hitSound;       // 打中火雞的聲音
    public AudioClip winSound;       // 通關勝利音效
    public AudioClip failSound;      // 失敗警報音效

    // ============================================
    // 背包獎勵設定
    // ============================================
    [Header("背包獎勵")]
    public backpack playerBackpack;
    public Sprite fuseIcon;

    // ============================================
    // 遊戲參數
    // ============================================
    [Header("遊戲參數")]
    public float gameDuration = 25f;       // 遊戲時間 (秒)
    public int targetScore = 6;            // 過關目標敲擊數
    public float minSpawnDelay = 0.6f;     // 生成間隔 (最短)
    public float maxSpawnDelay = 1.4f;     // 生成間隔 (最長)
    public float moleStayDuration = 1.5f;  // 火雞停留時間

    // ============================================
    // 遊戲狀態
    // ============================================
    [Header("遊戲狀態")]
    public bool isGameActive = false;
    public int currentScore = 0;
    private float timeRemaining = 0f;


    // ============================================
    // 初始化
    // ============================================
    [ContextMenu("測試開始遊戲")]
    void Start()
    {
        isGameActive = false;

        StartGame();
    }


    // ============================================
    // 開始遊戲
    // ============================================
    public void StartGame()
    {
        if (isGameActive) return;

        currentScore = 0;
        timeRemaining = gameDuration;
        isGameActive = true;

        UpdateUI();

        StartCoroutine(GameTimerRoutine());
        StartCoroutine(SpawnLoopRoutine());
    }


    // ============================================
    // 遊戲倒數
    // ============================================
    private IEnumerator GameTimerRoutine()
    {
        while (timeRemaining > 0 && isGameActive)
        {
            timeRemaining -= Time.deltaTime;

            UpdateUI();

            yield return null;
        }

        if (isGameActive)
        {
            // 時間到且未達標 → 失敗
            GameOver(false);
        }
    }


    // ============================================
    // 火雞生成
    // ============================================
    private IEnumerator SpawnLoopRoutine()
    {
        while (isGameActive)
        {
            yield return new WaitForSeconds(
                Random.Range(minSpawnDelay, maxSpawnDelay)
            );

            if (!isGameActive)
                break;

            // 隨機挑一隻目前不在上方的火雞冒出
            int randomIndex = Random.Range(
                0,
                turkeyMoles.Length
            );

            if (!turkeyMoles[randomIndex].IsUp)
            {
                turkeyMoles[randomIndex]
                    .PopUp(moleStayDuration);
            }
        }
    }


    // ============================================
    // 火雞被打中
    // ============================================
    /// <summary>
    /// 火雞被打中時由 TurkeyMole 呼叫
    /// </summary>
    public void AddScore()
    {
        if (!isGameActive)
            return;

        currentScore++;

        UpdateUI();

        if (currentScore >= targetScore)
        {
            GameOver(true);
        }
    }


    // ============================================
    // 錘子敲擊
    // ============================================
    public void HammerHit(int holeNumber)
    {
        if (!isGameActive)
            return;

        int index = holeNumber - 1;

        // 確認索引有效
        if (index < 0 || index >= turkeyMoles.Length)
            return;

        // 呼叫該火雞的 Hit() 判定擊中
        if (turkeyMoles[index].Hit())
        {
            // 播放擊中音效
            if (audioSource != null && hitSound != null)
            {
                audioSource.PlayOneShot(hitSound);
            }

            AddScore();
        }
    }


    // ============================================
    // 更新 UI
    // ============================================
    private void UpdateUI()
    {
        if (scoreText != null)
        {
            // 例如：001、002、006
            scoreText.text = currentScore.ToString("D3");
        }

        if (timerText != null)
        {
            timerText.text =
                Mathf.CeilToInt(
                    Mathf.Max(0, timeRemaining)
                ).ToString();
        }
    }


    // ============================================
    // 遊戲結束
    // ============================================
    private void GameOver(bool isSuccess)
    {
        isGameActive = false;

        StopAllCoroutines();

        if (isSuccess)
        {
            // ----------------------------------------
            // 播放成功音效
            // ----------------------------------------
            if (audioSource != null && winSound != null)
            {
                audioSource.PlayOneShot(winSound);
            }


            // ----------------------------------------
            // 通關獎勵：直接加入背包
            // ----------------------------------------
            if (playerBackpack != null)
            {
                bool added =
                    playerBackpack.AddItem(
                        "fuse",
                        fuseIcon
                    );

                if (added)
                {
                    Debug.Log(
                        "打地鼠通關！保險絲已加入背包！"
                    );
                }
                else
                {
                    Debug.LogWarning(
                        "打地鼠通關，但保險絲無法加入背包！"
                    );
                }
            }
            else
            {
                Debug.LogError(
                    "WhackAMoleManager 沒有指定 Player Backpack！"
                );
            }
        }
        else
        {
            // ----------------------------------------
            // 失敗
            // ----------------------------------------
            if (audioSource != null && failSound != null)
            {
                audioSource.PlayOneShot(failSound);
            }
        }
    }
}