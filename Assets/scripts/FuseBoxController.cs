using UnityEngine;
using System.Collections;

public class FuseBoxController : MonoBehaviour
{
    [Header("Player Backpack")]
    public backpack playerBackpack;

    [Header("Alarm")]
    public AudioSource alarmAudio;

    [Header("Door")]
    public Transform door;
    public float doorOpenHeight = 3f;
    public float doorOpenDuration = 2f;

    [Header("Delay")]
    public float doorOpenDelay = 30f;

    private bool activated = false;

    public void ActivateFuseBox()
    {
        if (activated)
        {
            Debug.Log("電箱已經啟動過了！");
            return;
        }

        if (playerBackpack == null)
        {
            Debug.LogWarning("FuseBox 沒有指定 Player Backpack！");
            return;
        }

        int fuseCount = playerBackpack.GetItemCount("fuse");

        Debug.Log("目前背包 Fuse 數量：" + fuseCount);

        if (fuseCount < 2)
        {
            Debug.Log("Fuse 不足！需要 2 個 Fuse。");
            return;
        }

        Debug.Log("Fuse ×2，電箱啟動！");

        activated = true;

        // 播放警報
        if (alarmAudio != null)
        {
            alarmAudio.Play();
        }

        Debug.Log("警報開始！");

        // 開始 30 秒倒數
        StartCoroutine(OpenDoorAfterDelay());
    }

    IEnumerator OpenDoorAfterDelay()
    {
        Debug.Log("30 秒倒數開始！");

        yield return new WaitForSeconds(doorOpenDelay);

        Debug.Log("30 秒到了！開始開門！");

        if (door != null)
        {
            StartCoroutine(OpenDoor());
        }
        else
        {
            Debug.LogWarning("FuseBox 沒有指定 Door！");
        }
    }

    IEnumerator OpenDoor()
    {
        Vector3 startPosition = door.position;
        Vector3 targetPosition =
            startPosition + Vector3.up * doorOpenHeight;

        float elapsed = 0f;

        while (elapsed < doorOpenDuration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / doorOpenDuration;

            door.position = Vector3.Lerp(
                startPosition,
                targetPosition,
                t
            );

            yield return null;
        }

        door.position = targetPosition;

        Debug.Log("門已完全升起！");
    }
}