using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class UDPGestureReceiver : MonoBehaviour
{
    [Header("UDP Settings")]
    public int port = 5005;

    [Header("Gesture Timeouts")]
    public float runTimeout = 0.3f;
    public float walkTimeout = 0.3f;
    public float turnTimeout = 0.15f;
    public float moveTimeout = 0.2f;

    [Header("Interaction Settings")]
    public float interactCooldown = 1.5f;
    private float lastInteractTime = -100f;

    [Header("Action Cooldowns")]
    public float grabCooldown = 1.5f;
    public float uvCooldown = 0.5f;
    public float slotCooldown = 0.5f;

    private float lastRunTime = -1f;
    private float lastWalkTime = -1f;
    private float lastTurnLeftTime = -1f;
    private float lastTurnRightTime = -1f;
    private float lastMoveTime = -1f;

    private float lastGrabTime = -100f;
    private float lastUvTime = -100f;
    private float lastSlotTime = -100f;

    [Header("Pickup Settings")]
    public float pickupRange = 5f;
    public Camera playerCamera;
    public float sphereCastRadius = 0.4f;

    [Header("Crosshair Settings")]
    public UnityEngine.UI.Image crosshairUI;

    public Color defaultColor = new Color(1f, 1f, 1f, 0.5f);
    public Color hoverColor = Color.red;

    public Vector2 defaultSize = new Vector2(6f, 6f);
    public Vector2 hoverSize = new Vector2(12f, 12f);

    [Header("Flashlight Switching")]
    public GameObject flashlight_;
    public GameObject flashlight_uv_copy;

    public FlashlightBattery flashlightBattery;

    [Header("Inventory")]
    public backpack playerBackpack;
    public int currentSlot = 1;

    [Header("Generator Mini-Game")]
    public bool generatorActive = false;
    public string latestGesture = "";

    private UdpClient udpClient;
    private Thread receiveThread;

    private string latestMessage = "";
    private bool newMessageAvailable = false;

    private readonly object lockObj = new object();

    private float turnSpeedFactor = 0f;

    private FirstPersonMovement movement;

    // 打地鼠機
    public WhackAMoleManager moleManager;


    // =========================================================
    // START
    // =========================================================

    void Start()
    {
        movement = GetComponent<FirstPersonMovement>();

        udpClient = new UdpClient(port);

        receiveThread = new Thread(ReceiveLoop)
        {
            IsBackground = true
        };

        receiveThread.Start();
    }


    // =========================================================
    // DESTROY
    // =========================================================

    void OnDestroy()
    {
        receiveThread?.Abort();
        udpClient?.Close();
    }


    // =========================================================
    // UDP RECEIVE
    // =========================================================

    void ReceiveLoop()
    {
        IPEndPoint remoteEP =
            new IPEndPoint(IPAddress.Any, 0);

        while (true)
        {
            try
            {
                byte[] data =
                    udpClient.Receive(ref remoteEP);

                string msg =
                    Encoding.UTF8.GetString(data).Trim();

                lock (lockObj)
                {
                    latestMessage = msg;
                    newMessageAvailable = true;
                }
            }
            catch (Exception e)
            {
                if (!(e is ThreadAbortException))
                {
                    Debug.LogWarning(
                        "UDP receive error: " +
                        e.Message
                    );
                }

                break;
            }
        }
    }


    // =========================================================
    // UPDATE
    // =========================================================

    void Update()
    {
        UpdateCrosshair();


        // =====================================================
        // MOVEMENT GESTURE STATUS
        // =====================================================

        if (movement != null)
        {
            movement.gestureRunning =
                (Time.time - lastRunTime) < runTimeout;

            movement.gestureWalking =
                (Time.time - lastWalkTime) < walkTimeout;

            movement.gestureTurnLeft =
                (Time.time - lastTurnLeftTime) < turnTimeout;

            movement.gestureTurnRight =
                (Time.time - lastTurnRightTime) < turnTimeout;

            movement.gestureTurnFactor =
                turnSpeedFactor;


            // Movement timeout protection
            if (Time.time - lastMoveTime > moveTimeout)
            {
                movement.forwardInput = 0f;
                movement.rightInput = 0f;
            }
        }


        // =====================================================
        // PROCESS UDP MESSAGES
        // =====================================================

        while (true)
        {
            string msg;

            lock (lockObj)
            {
                if (!newMessageAvailable)
                    break;

                msg = latestMessage;
                newMessageAvailable = false;
            }


            // =================================================
            // GENERATOR MINI-GAME
            // =================================================

            if (generatorActive)
            {
                if (msg == "PALM")
                {
                    Debug.Log(
                        ">>> GENERATOR PALM ACCEPTED <<<"
                    );

                    latestGesture = "PALM";
                }

                // Generator only accepts PALM
                continue;
            }


            // =================================================
            // MOVEMENT
            // =================================================

            if (movement == null)
                return;


            if (msg.StartsWith("MOVE F:"))
            {
                ParseMoveMessage(msg);
            }


            if (msg.Contains("RUN"))
            {
                lastRunTime = Time.time;
            }


            if (msg.Contains("WALK"))
            {
                lastWalkTime = Time.time;
            }


            if (msg.Contains("TURN LEFT"))
            {
                lastTurnLeftTime = Time.time;
                turnSpeedFactor = ParseFactor(msg);
            }


            if (msg.Contains("TURN RIGHT"))
            {
                lastTurnRightTime = Time.time;
                turnSpeedFactor = ParseFactor(msg);
            }


            if (msg == "TURN STOP")
            {
                turnSpeedFactor = 0f;
            }


            // =================================================
            // GRAB
            // =================================================

            if (msg.Contains("GRAB") &&
                Time.time - lastGrabTime > grabCooldown)
            {
                lastGrabTime = Time.time;

                TryInteract();
            }


            // =================================================
            // INVENTORY / WHACK A MOLE
            // =================================================

            if (msg == "1")
            {
                if (moleManager != null && moleManager.isGameActive)
                {
                    moleManager.HammerHit(1);
                }
                else
                {
                    SelectSlot(1);
                }
            }

            if (msg == "2")
            {
                if (moleManager != null && moleManager.isGameActive)
                {
                    moleManager.HammerHit(2);
                }
                else
                {
                    SelectSlot(2);
                }
            }

            if (msg == "3")
            {
                if (moleManager != null && moleManager.isGameActive)
                {
                    moleManager.HammerHit(3);
                }
                else
                {
                    SelectSlot(3);
                }
            }

            if (msg == "4")
            {
                if (moleManager != null && moleManager.isGameActive)
                {
                    moleManager.HammerHit(4);
                }
                else
                {
                    SelectSlot(4);
                }
            }

            if (msg.StartsWith("SLOT "))
            {
                if (int.TryParse(msg.Substring(5), out int slot))
                {
                    if (moleManager != null && moleManager.isGameActive && slot >= 1 && slot <= 4)
                    {
                        // 打地鼠遊戲中 → 打對應的洞
                        Debug.Log("打地鼠模式 → HammerHit(" + slot + ")");
                        moleManager.HammerHit(slot);
                    }
                    else
                    {
                        // 平常遊戲 → 切換背包
                        SelectSlot(slot);
                    }
                }
            }
        }
    }


    // =========================================================
    // MOVEMENT
    // =========================================================

    void ParseMoveMessage(string msg)
    {
        try
        {
            string[] parts =
                msg.Split(' ');

            float f = 0f;
            float r = 0f;


            foreach (string part in parts)
            {
                if (part.StartsWith("F:"))
                {
                    float.TryParse(
                        part.Substring(2),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out f
                    );
                }
                else if (part.StartsWith("R:"))
                {
                    float.TryParse(
                        part.Substring(2),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out r
                    );
                }
            }


            movement.forwardInput = f;
            movement.rightInput = r;

            lastMoveTime = Time.time;
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                "Failed to parse MOVE message: " +
                msg +
                " | " +
                e.Message
            );
        }
    }


    float ParseFactor(string msg)
    {
        int idx =
            msg.IndexOf(':');


        if (idx >= 0 &&
            float.TryParse(
                msg.Substring(idx + 1),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float v))
        {
            return Mathf.Clamp(v, 0f, 1f);
        }


        return 1f;
    }


    // =========================================================
    // CROSSHAIR
    // =========================================================

    void UpdateCrosshair()
    {
        if (crosshairUI == null ||
            playerCamera == null)
            return;


        Vector3 origin =
            playerCamera.transform.position;

        Vector3 direction =
            playerCamera.transform.forward;


        RaycastHit hit;

        bool isLookingAtInteractable = false;


        if (Physics.SphereCast(
            origin,
            sphereCastRadius,
            direction,
            out hit,
            pickupRange))
        {
            bool isLever =
                hit.collider.GetComponentInParent<lever_controller>() != null;

            bool isPickable =
                hit.collider.CompareTag("Pickable");


            bool isGenerator =
                hit.collider.GetComponentInParent<GeneratorInteract>() != null;

            bool isMoleMachine =
                hit.collider.CompareTag("MoleMachine");

            if (isLever ||
                isPickable ||
                isGenerator ||
                isMoleMachine)
            {
                isLookingAtInteractable = true;
            }
        }


        if (isLookingAtInteractable)
        {
            crosshairUI.color = hoverColor;

            crosshairUI.rectTransform.sizeDelta =
                hoverSize;
        }
        else
        {
            crosshairUI.color = defaultColor;

            crosshairUI.rectTransform.sizeDelta =
                defaultSize;
        }
    }


    // =========================================================
    // INTERACTION / PICKUP
    // =========================================================


void TryInteract()
{
    if (Time.time - lastInteractTime < interactCooldown)
        return;

    // =========================================
    // 0. 使用背包中的電池
    // 只有選中 battery 時，比讚才會優先補電
    // =========================================
    if (playerBackpack != null &&
        playerBackpack.GetSelectedItem() == "battery")
    {
        if (playerBackpack.GetItemCount("battery") <= 0)
        {
            Debug.Log("背包沒有電池！");
            return;
        }

        if (flashlightBattery == null)
        {
            Debug.LogWarning("尚未指定 FlashlightBattery 元件！");
            return;
        }

        if (flashlightBattery.currentBattery >=
            flashlightBattery.maxBattery)
        {
            Debug.Log("白光手電筒電量已滿，不消耗電池！");
            return;
        }

        // 先確認補電成功，再消耗一顆電池
        if (flashlightBattery.TryAddBattery())
        {
            playerBackpack.UseItemByName("battery");
            lastInteractTime = Time.time;
        }

        return;
    }

    // =========================================
    // 1. 確認玩家攝影機
    // =========================================
    if (playerCamera == null)
    {
        Debug.LogWarning("playerCamera not assigned.");
        return;
    }

    Vector3 origin = playerCamera.transform.position;
    Vector3 direction = playerCamera.transform.forward;

    Ray ray = new Ray(origin, direction);
    RaycastHit hit;

    Debug.DrawRay(origin, direction * pickupRange, Color.red, 2f);

    if (!Physics.Raycast(ray, out hit, pickupRange))
    {
        Debug.Log("揮空了，眼前沒東西。");
        return;
    }

    // =========================================
    // 2. GENERATOR
    // =========================================
    GeneratorInteract generator =
        hit.collider.GetComponentInParent<GeneratorInteract>();

    if (generator != null)
    {
        Debug.Log("GRAB → GENERATOR");
        generator.Activate();

        lastInteractTime = Time.time;
        return;
    }

    // =========================================
    // 3. 打地鼠機
    // =========================================
    if (hit.collider.CompareTag("MoleMachine"))
    {
        if (moleManager != null && !moleManager.isGameActive)
        {
            if (playerBackpack == null)
            {
                Debug.LogWarning("沒有指定 Player Backpack！");
                return;
            }

            if (playerBackpack.GetSelectedItem() != "coin")
            {
                Debug.Log("請先在背包選擇 coin！");
                return;
            }

            if (playerBackpack.GetSelectedItemCount() <= 0)
            {
                Debug.Log("沒有 coin，無法開始打地鼠！");
                return;
            }

            if (!playerBackpack.UseSelectedItem())
            {
                Debug.Log("硬幣使用失敗！");
                return;
            }

            Debug.Log("已使用 1 枚 coin，開始打地鼠遊戲！");
            moleManager.StartGame();
        }

        lastInteractTime = Time.time;
        return;
    }

    // =========================================
    // 4. 火雞地鼠
    // =========================================
    if (hit.collider.CompareTag("Mole"))
    {
        TurkeyMole mole =
            hit.collider.GetComponentInParent<TurkeyMole>();

        if (mole != null)
        {
            Debug.Log("比讚打中火雞！");
            mole.OnHitByPlayer();

            lastInteractTime = Time.time;
            return;
        }
    }

    // =========================================
    // 5. 冷凍庫門
    // =========================================
    RotatingDoor door =
        hit.collider.GetComponentInParent<RotatingDoor>();

    if (door != null)
    {
        Debug.Log("比讚 → 冷凍庫門");
        door.OpenFromGesture();

        lastInteractTime = Time.time;
        return;
    }

    // =========================================
    // 6. 電箱
    // =========================================
    FuseBoxController fuseBox =
        hit.collider.GetComponentInParent<FuseBoxController>();

    if (fuseBox != null)
    {
        Debug.Log("比讚 → 電箱");
        fuseBox.ActivateFuseBox();

        lastInteractTime = Time.time;
        return;
    }

    // =========================================
    // 7. 冷凍庫拉桿
    // =========================================
    lever_controller lever =
        hit.collider.GetComponent<lever_controller>();

    if (lever == null)
    {
        lever = hit.collider.GetComponentInParent<lever_controller>();
    }

    if (lever != null)
    {
        Debug.Log("操作冷凍庫拉桿：" + lever.gameObject.name);
        lever.ToggleLever();

        lastInteractTime = Time.time;
        return;
    }

    // =========================================
    // 8. 大門拉桿
    // =========================================
    GateLeverController gateLever =
        hit.collider.GetComponent<GateLeverController>();

    if (gateLever == null)
    {
        gateLever =
            hit.collider.GetComponentInParent<GateLeverController>();
    }

    if (gateLever != null)
    {
        Debug.Log("操作大門拉桿：" + gateLever.gameObject.name);
        gateLever.ToggleLever();

        lastInteractTime = Time.time;
        return;
    }

    // =========================================
    // 9. 撿取物品
    // =========================================
    if (hit.collider.CompareTag("Pickable"))
    {
        backpackitem item =
            hit.collider.GetComponentInParent<backpackitem>();

        if (item == null)
        {
            Debug.LogWarning(
                "此 Pickable 物件沒有 backpackitem：" +
                hit.collider.gameObject.name
            );
            return;
        }

        if (playerBackpack == null)
        {
            Debug.LogWarning("Player Backpack is not assigned!");
            return;
        }

        Debug.Log("準備加入背包：" + item.itemName);

        bool added = playerBackpack.AddItem(
            item.itemName,
            item.itemIcon
        );

        if (added)
        {
            Debug.Log("撿起物品：" + item.itemName);

            // 撿到紫光手電筒時，切換手電筒
            if (item.itemName == "flashlight")
            {
                if (flashlight_ != null)
                    flashlight_.SetActive(false);

                if (flashlight_uv_copy != null)
                    flashlight_uv_copy.SetActive(true);

                playerBackpack.SelectSlot(1);

                Debug.Log("已切換到紫光手電筒！");
            }

            Destroy(item.gameObject);
            lastInteractTime = Time.time;
        }
        else
        {
            Debug.Log("背包已滿，無法撿取：" + item.itemName);
        }

        return;
    }

    // =========================================
    // 10. 其他物件
    // =========================================
    Debug.Log("看到東西，但不能互動：" + hit.collider.gameObject.name);
}


    // =========================================================
    // INVENTORY SLOT
    // =========================================================

    void SelectSlot(int slot)
    {
        currentSlot = slot;

        if (playerBackpack != null)
        {
            playerBackpack.SelectSlot(slot);
        }

        Debug.Log("Inventory slot selected: " + slot);
    }
}