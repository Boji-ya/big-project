using System;
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
    public float runTimeout = 0.3f;  // seconds to keep RUN true after last message
    public float walkTimeout = 0.3f;  // seconds to keep WALK true after last message
    public float turnTimeout = 0.15f; // seconds to keep TURN true after last message

    [Header("Interaction Settings")]
    public float interactCooldown = 1.5f; // 互動冷卻時間 (1.5秒內不重複觸發)
    private float lastInteractTime = -100f; // 紀錄上一次互動的時間

    [Header("Action Cooldowns (防止重複觸發)")]
    public float grabCooldown = 1.5f;
    public float uvCooldown = 0.5f; // 手電筒開關防連發
    public float slotCooldown = 0.5f; // 切換道具防連發

    private float lastRunTime = -1f;
    private float lastWalkTime = -1f;
    private float lastTurnLeftTime = -1f;
    private float lastTurnRightTime = -1f;

    private float lastGrabTime = -100f;
    private float lastUvTime = -100f;
    private float lastSlotTime = -100f;

    [Header("Pickup Settings")]
    public float pickupRange = 5f;
    public Camera playerCamera;

    // 射線粗度（讓玩家更好瞄準）
    public float sphereCastRadius = 0.4f;

    // 動態準心設定
    [Header("Crosshair Settings")]
    public UnityEngine.UI.Image crosshairUI;
    public Color defaultColor = new Color(1f, 1f, 1f, 0.5f);
    public Color hoverColor = Color.red;
    public Vector2 defaultSize = new Vector2(6f, 6f);
    public Vector2 hoverSize = new Vector2(12f, 12f);

    [Header("UV Light")]
    public GameObject uvLight;

    [Header("Inventory")]
    public int currentSlot = 1;

    // ── private networking ──────────────────────────────────────────────
    private UdpClient udpClient;
    private Thread receiveThread;
    private string latestMessage = "";
    private bool newMessageAvailable = false;
    private readonly object lockObj = new object();

    private FirstPersonMovement movement;

    void Start()
    {
        movement = GetComponent<FirstPersonMovement>();

        udpClient = new UdpClient(port);
        receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
        receiveThread.Start();
    }

    void OnDestroy()
    {
        receiveThread?.Abort();
        udpClient?.Close();
    }

    void ReceiveLoop()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEP);
                string msg = Encoding.UTF8.GetString(data).Trim();
                lock (lockObj)
                {
                    latestMessage = msg;
                    newMessageAvailable = true;
                }
            }
            catch (Exception e)
            {
                if (!(e is ThreadAbortException))
                    Debug.LogWarning("UDP receive error: " + e.Message);
                break;
            }
        }
    }

    void Update()
    {
        UpdateCrosshair();

        movement.gestureRunning = (Time.time - lastRunTime) < runTimeout;
        movement.gestureWalking = (Time.time - lastWalkTime) < walkTimeout;
        movement.gestureTurnLeft = (Time.time - lastTurnLeftTime) < turnTimeout;
        movement.gestureTurnRight = (Time.time - lastTurnRightTime) < turnTimeout;

        while (true)
        {
            string msg;
            lock (lockObj)
            {
                if (!newMessageAvailable) break;
                msg = latestMessage;
                newMessageAvailable = false;
            }

            if (movement == null) return;

            if (msg.Contains("RUN")) lastRunTime = Time.time;
            if (msg.Contains("WALK")) lastWalkTime = Time.time;
            if (msg.Contains("TURN LEFT")) lastTurnLeftTime = Time.time;
            if (msg.Contains("TURN RIGHT")) lastTurnRightTime = Time.time;

            if (msg.Contains("GRAB") && Time.time - lastGrabTime > grabCooldown)
            {
                lastGrabTime = Time.time;
                TryInteract();
            }
            if (msg.Contains("UV") && Time.time - lastUvTime > uvCooldown)
            {
                lastUvTime = Time.time;
                ToggleUVLight();
            }
            if (msg == "1") SelectSlot(1);
            if (msg == "2") SelectSlot(2);
            if (msg == "3") SelectSlot(3);
            if (msg == "4") SelectSlot(4);
        }
    }

    void UpdateCrosshair()
    {
        if (crosshairUI == null || playerCamera == null) return;

        Vector3 origin = playerCamera.transform.position;
        Vector3 direction = playerCamera.transform.forward;
        RaycastHit hit;

        bool isLookingAtInteractable = false;

        if (Physics.SphereCast(origin, sphereCastRadius, direction, out hit, pickupRange))
        {
            //Debug.Log(" 準心打中了：" + hit.collider.gameObject.name);

            bool isLever = hit.collider.GetComponentInParent<lever_controller>() != null;
            bool isPickable = hit.collider.CompareTag("Pickable");

            if (isLever || isPickable)
            {
                isLookingAtInteractable = true;
            }
        }

        if (isLookingAtInteractable)
        {
            crosshairUI.color = hoverColor;
            crosshairUI.rectTransform.sizeDelta = hoverSize;
        }
        else
        {
            crosshairUI.color = defaultColor;
            crosshairUI.rectTransform.sizeDelta = defaultSize;
        }
    }

    void TryInteract()
    {
        if (Time.time - lastInteractTime < interactCooldown)
        {
            return;
        }

        if (playerCamera == null) { Debug.LogWarning("playerCamera not assigned."); return; }

        Vector3 origin = playerCamera.transform.position;
        Vector3 direction = playerCamera.transform.forward;
        Ray ray = new Ray(origin, direction);
        RaycastHit hit;

        Debug.DrawRay(origin, direction * pickupRange, Color.red, 2f);

        if (Physics.Raycast(ray, out hit, pickupRange))
        {
            lever_controller lever = hit.collider.GetComponent<lever_controller>();
            if (lever == null) lever = hit.collider.GetComponentInParent<lever_controller>();

            if (lever != null)
            {
                Debug.Log("操作機關：" + lever.gameObject.name);
                lever.ToggleLever();
                return;
            }

            if (hit.collider.CompareTag("Pickable"))
            {
                Debug.Log("撿起物品：" + hit.collider.gameObject.name);
                Destroy(hit.collider.gameObject);
                return;
            }

            Debug.Log("看到東西，但不能互動：" + hit.collider.gameObject.name);
        }
        else
        {
            Debug.Log("揮空了，眼前沒東西。");
        }
    }

    void ToggleUVLight()
    {
        if (uvLight == null) { Debug.LogWarning("uvLight not assigned in Inspector."); return; }
        uvLight.SetActive(!uvLight.activeSelf);
        Debug.Log("UV light: " + (uvLight.activeSelf ? "ON" : "OFF"));
    }

    void SelectSlot(int slot)
    {
        currentSlot = slot;
        Debug.Log("Inventory slot selected: " + slot);
    }
}