using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class udp_test : MonoBehaviour
{
    UdpClient udp;

    public FirstPersonMovement movement; // 【新增】記得在Inspector裡把角色物件拖進來

    void Start()
    {
        udp = new UdpClient(5005);
        Debug.Log("UDP start");
    }

    void Update()
    {
        if (udp.Available > 0)
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 5005);
            byte[] data = udp.Receive(ref remoteEP);
            string message = Encoding.UTF8.GetString(data);
            Debug.Log("received: " + message);
            HandleMessage(message); // 【新增】解析指令並控制角色
        }
    }

    void HandleMessage(string message)
    {
        if (movement == null) return;

        if (message == "WALK")
        {
            movement.gestureWalking = true;
            movement.gestureRunning = false;
        }
        else if (message == "RUN")
        {
            movement.gestureRunning = true;
            movement.gestureWalking = false;
        }
        else if (message == "STOP")
        {
            movement.gestureWalking = false;
            movement.gestureRunning = false;
            movement.gestureTurnLeft = false;
            movement.gestureTurnRight = false;
        }
        else if (message == "TURN STOP")
        {
            movement.gestureTurnLeft = false;
            movement.gestureTurnRight = false;
        }
        else if (message.StartsWith("TURN LEFT:"))
        {
            movement.gestureTurnLeft = true;
            movement.gestureTurnRight = false;
        }
        else if (message.StartsWith("TURN RIGHT:"))
        {
            movement.gestureTurnRight = true;
            movement.gestureTurnLeft = false;
        }
        else if (message == "1" || message == "2" || message == "3" || message == "4")
        {
            // TODO: 切換背包欄位
        }
        else if (message == "UV")
        {
            // TODO: 暈眩火雞王
        }
        else if (message == "GRAB")
        {
            // TODO: 拾取物品/啟動機關
        }
    }

    void OnApplicationQuit()
    {
        udp.Close();
    }
}