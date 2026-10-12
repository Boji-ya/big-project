using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GeneratorMiniGame : MonoBehaviour
{
    [Header("Needle")]
    public generatorpointer pointer;

    [Tooltip("Empty child GameObject placed at the visual tip of the needle sprite.")]
    public RectTransform needleTip;


    [Header("Canvas / Camera")]
    [Tooltip("Leave null for Screen Space - Overlay.")]
    public Camera uiCamera;


    [Header("Targets")]
    public RectTransform[] targets;


    [Header("Gesture")]
    public UDPGestureReceiver gestureReceiver;


    [Header("UI")]
    public TMP_Text gestureText;
    public TMP_Text resultText;


    [Header("Generator Progress")]
    public Slider progressBar;

    public float progressPerSuccess = 25f;
    public float progressPerFail = 25f;

    private float generatorProgress = 0f;


    [Header("Round Settings")]
    public float nextRoundDelay = 1f;


    [Header("Pointer Speed By Progress")]
    [Tooltip("Speed at 0%, 25%, 50%, and 75% progress.")]
    public float[] pointerSpeedByProgress =
        new float[] { 100f, 150f, 200f, 250f };


    // =========================================================
    // PRIVATE VARIABLES
    // =========================================================

    private string requiredGesture = "PALM";

    private RectTransform currentTarget;

    private bool roundCompleted = false;

    private bool generatorFinished = false;


    // =========================================================
    // ACTIVATE GENERATOR
    // =========================================================

    public void ActivateGenerator()
    {
        if (generatorFinished)
            return;


        gameObject.SetActive(true);


        // Enable generator mode
        if (gestureReceiver != null)
        {
            gestureReceiver.generatorActive = true;
            gestureReceiver.latestGesture = "";
        }


        // Reset progress
        generatorProgress = 0f;


        if (progressBar != null)
        {
            progressBar.minValue = 0f;
            progressBar.maxValue = 100f;
            progressBar.value = generatorProgress;
        }


        // Make sure pointer is enabled
        if (pointer != null)
        {
            pointer.enabled = true;
        }


        StartNewRound();


        Debug.Log(
            "GENERATOR MINIGAME STARTED"
        );
    }


    // =========================================================
    // START
    // =========================================================

    void Start()
    {
        gameObject.SetActive(false);


        generatorProgress = 0f;


        if (progressBar != null)
        {
            progressBar.minValue = 0f;
            progressBar.maxValue = 100f;
            progressBar.value = generatorProgress;
        }
    }


    // =========================================================
    // UPDATE
    // =========================================================

    void Update()
    {
        Debug.Log($"roundCompleted={roundCompleted} finished={generatorFinished} pointer={pointer != null} gr={gestureReceiver != null} tip={needleTip != null} target={currentTarget != null} gesture={gestureReceiver?.latestGesture}");
        if (roundCompleted ||
            generatorFinished)
            return;


        if (pointer == null ||
            gestureReceiver == null ||
            needleTip == null ||
            currentTarget == null)
        {
            return;
        }


        string gesture =
            gestureReceiver.latestGesture;


        // =====================================================
        // NO GESTURE
        // =====================================================

        if (string.IsNullOrEmpty(gesture))
        {
            return;
        }


        Debug.Log(
            "MINIGAME CHECK → Required: " +
            requiredGesture +
            " | Received: " +
            gesture
        );


        // =====================================================
        // PALM RECEIVED
        // =====================================================

        if (gesture == requiredGesture)
        {
            // Check whether needle is currently inside target

            if (IsNeedleInsideTarget())
            {
                Debug.Log(
                    "PALM INSIDE TARGET → SUCCESS"
                );

                Success();
            }
            else
            {
                Debug.Log(
                    "PALM OUTSIDE TARGET → FAIL"
                );

                Fail();
            }


            // Clear gesture so the same PALM
            // doesn't trigger repeatedly
            gestureReceiver.latestGesture = "";
        }
    }


    // =========================================================
    // START NEW ROUND
    // =========================================================

    void StartNewRound()
    {
        roundCompleted = false;


        // =====================================================
        // POINTER SPEED
        // =====================================================

        UpdatePointerSpeed();


        // =====================================================
        // HIDE ALL TARGETS
        // =====================================================

        if (targets != null)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                {
                    targets[i].gameObject.SetActive(false);
                }
            }
        }


        // =====================================================
        // CHECK TARGETS
        // =====================================================

        if (targets == null ||
            targets.Length == 0)
        {
            Debug.LogWarning(
                "No targets assigned to GeneratorMiniGame."
            );

            return;
        }


        // =====================================================
        // RANDOM TARGET
        // =====================================================

        int targetIndex =
            Random.Range(
                0,
                targets.Length
            );


        currentTarget =
            targets[targetIndex];


        if (currentTarget != null)
        {
            currentTarget.gameObject.SetActive(true);
        }


        // =====================================================
        // REQUIRED GESTURE
        // =====================================================

        requiredGesture = "PALM";


        // =====================================================
        // UI
        // =====================================================

        if (gestureText != null)
        {
            gestureText.text =
                "SHOW : OPEN PALM";
        }


        if (resultText != null)
        {
            resultText.text = "";
        }


        // Clear old gesture

        if (gestureReceiver != null)
        {
            gestureReceiver.latestGesture = "";
        }


        Debug.Log(
            "NEW ROUND → Target " +
            (targetIndex + 1) +
            " | Gesture PALM" +
            " | Progress " +
            generatorProgress +
            "%"
        );
    }


    // =========================================================
    // UPDATE POINTER SPEED
    // =========================================================

    void UpdatePointerSpeed()
    {
        if (pointer == null)
            return;


        if (pointerSpeedByProgress == null ||
            pointerSpeedByProgress.Length == 0)
            return;


        int index =
            Mathf.FloorToInt(
                generatorProgress / 25f
            );


        index =
            Mathf.Clamp(
                index,
                0,
                pointerSpeedByProgress.Length - 1
            );


        pointer.speed =
            pointerSpeedByProgress[index];


        Debug.Log(
            "Progress: " +
            generatorProgress +
            "% → Pointer Speed: " +
            pointer.speed
        );
    }


    // =========================================================
    // CHECK NEEDLE TARGET
    // =========================================================

    bool IsNeedleInsideTarget()
    {
        if (currentTarget == null ||
            needleTip == null)
        {
            return false;
        }


        Vector2 screenPoint =
            RectTransformUtility.WorldToScreenPoint(
                uiCamera,
                needleTip.position
            );


        bool inside =
            RectTransformUtility.RectangleContainsScreenPoint(
                currentTarget,
                screenPoint,
                uiCamera
            );


        return inside;
    }


    // =========================================================
    // SUCCESS
    // =========================================================

    void Success()
    {
        roundCompleted = true;


        // =====================================================
        // ADD PROGRESS
        // =====================================================

        generatorProgress +=
            progressPerSuccess;


        generatorProgress =
            Mathf.Clamp(
                generatorProgress,
                0f,
                100f
            );


        // Update pointer speed
        UpdatePointerSpeed();


        // Update progress bar
        UpdateProgressBar();


        // =====================================================
        // RESULT
        // =====================================================

        if (resultText != null)
        {
            resultText.text =
                "CIRCUIT STABILIZED\n+" +
                progressPerSuccess +
                "%";
        }


        Debug.Log(
            "SUCCESS! Generator Progress: " +
            generatorProgress +
            "%"
        );


        // =====================================================
        // FINISHED?
        // =====================================================

        if (generatorProgress >= 100f)
        {
            Invoke(
                nameof(GeneratorComplete),
                0.8f
            );
        }
        else
        {
            Invoke(
                nameof(StartNextRound),
                nextRoundDelay
            );
        }
    }


    // =========================================================
    // FAIL
    // =========================================================

    void Fail()
    {
        roundCompleted = true;


        // =====================================================
        // REMOVE PROGRESS
        // =====================================================

        generatorProgress -=
            progressPerFail;


        generatorProgress =
            Mathf.Clamp(
                generatorProgress,
                0f,
                100f
            );


        // Reset pointer speed
        UpdatePointerSpeed();


        // Update progress bar
        UpdateProgressBar();


        // =====================================================
        // RESULT
        // =====================================================

        if (resultText != null)
        {
            resultText.text =
                "CIRCUIT FAILURE\n-" +
                progressPerFail +
                "%";
        }


        Debug.Log(
            "FAIL! Generator Progress: " +
            generatorProgress +
            "%"
        );


        // Clear gesture

        if (gestureReceiver != null)
        {
            gestureReceiver.latestGesture = "";
        }


        // =====================================================
        // NEXT ROUND
        // =====================================================

        Invoke(
            nameof(StartNextRound),
            nextRoundDelay
        );
    }


    // =========================================================
    // UPDATE PROGRESS BAR
    // =========================================================

    void UpdateProgressBar()
    {
        if (progressBar != null)
        {
            progressBar.value =
                generatorProgress;
        }
    }


    // =========================================================
    // NEXT ROUND
    // =========================================================

    void StartNextRound()
    {
        if (generatorFinished)
            return;


        StartNewRound();
    }


    // =========================================================
    // GENERATOR COMPLETE
    // =========================================================

    void GeneratorComplete()
    {
        generatorFinished = true;


        // =====================================================
        // UI
        // =====================================================

        if (resultText != null)
        {
            resultText.text =
                "GENERATOR COMPLETE!";
        }


        if (gestureText != null)
        {
            gestureText.text = "";
        }


        Debug.Log(
            "GENERATOR COMPLETE - GIVE FUSE"
        );


        // =====================================================
        // DISABLE GENERATOR MODE
        // =====================================================

        if (gestureReceiver != null)
        {
            gestureReceiver.generatorActive =
                false;

            gestureReceiver.latestGesture =
                "";
        }


        // =====================================================
        // STOP POINTER
        // =====================================================

        if (pointer != null)
        {
            pointer.enabled = false;
        }


        // =====================================================
        // HIDE TARGETS
        // =====================================================

        if (targets != null)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                {
                    targets[i].gameObject.SetActive(false);
                }
            }
        }


        // =====================================================
        // TODO: GIVE PLAYER FUSE
        // =====================================================

        // Add fuse/backpack code here later.
    }
}