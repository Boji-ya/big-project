
using System.Collections.Generic;
using UnityEngine;
public class FirstPersonMovement : MonoBehaviour
{
    public bool gestureRunning;
    public bool gestureWalking;
    public bool gestureTurnLeft;
    public bool gestureTurnRight;
    public float speed = 5;
    public float runSpeed = 9;
    public float turnSpeed = 60f;
    public float acceleration = 8f;
    public float turnAcceleration = 10f;
    [Header("Running")]
    public bool canRun = true;
    public bool IsRunning { get; private set; }
    private float currentSpeed = 0f;
    private float currentTurnSpeed = 0f;
    Rigidbody rb;
    public List<System.Func<float>> speedOverrides = new List<System.Func<float>>();
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }
    void FixedUpdate()
    {
        IsRunning = canRun && gestureRunning;
        float targetMovingSpeed = (gestureWalking || gestureRunning)
            ? (IsRunning ? runSpeed : speed)
            : 0f;
        if (speedOverrides.Count > 0)
            targetMovingSpeed = speedOverrides[speedOverrides.Count - 1]();
        currentSpeed = Mathf.Lerp(currentSpeed, targetMovingSpeed, acceleration * Time.fixedDeltaTime);
        float targetTurn = 0f;
        if (gestureTurnLeft) targetTurn = -turnSpeed;
        if (gestureTurnRight) targetTurn = turnSpeed;
        currentTurnSpeed = Mathf.Lerp(currentTurnSpeed, targetTurn, turnAcceleration * Time.fixedDeltaTime);
        transform.Rotate(0f, currentTurnSpeed * Time.fixedDeltaTime, 0f);
        rb.linearVelocity = transform.rotation * new Vector3(0, rb.linearVelocity.y, currentSpeed);
    }
}