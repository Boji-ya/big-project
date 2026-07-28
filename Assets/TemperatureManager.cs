using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // 控制 UI
using UnityEngine.SceneManagement; // 重新載入場景

public class TemperatureManager : MonoBehaviour
{
    public float currentTemp = 100f;
    public float dropSpeed= 0.5f;

    public Image frostUI;            // 結霜特效
    public AudioSource breathSound;  // 急促呼吸聲的 AudioSource

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        currentTemp -= Time.deltaTime * dropSpeed;

        update_freeze_effect();
        update_breahe_audio();


        if(currentTemp <= 0)
        {
            die();
        }

    }
    private void update_freeze_effect() {
        if (currentTemp <= 50)
        {
            //alpha 是照片透明度 temp越低  alpha越高 照片越清楚
            float alpha = (50 - currentTemp) / 55;

            Color tempColor = frostUI.color;
            tempColor.a = alpha;
            frostUI.color = tempColor;
        }
    }
    void update_breahe_audio() {
        if (currentTemp <= 25)

        {   //當前尚未播音效->播音效
            if (!breathSound.isPlaying)
            {
                breathSound.Play();
            }
        }
        else //未來如果有加回溫機制 就要停止播放音效
        {
            if (breathSound.isPlaying)
            {
                breathSound.Stop();
            }
        }

    }
    private void die()
    {
        // 重新載入目前的場景
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
