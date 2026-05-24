using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class udp_test : MonoBehaviour
{
    UdpClient udp;

    void Start()
    {
        udp = new UdpClient(5005);

        Debug.Log("UDP 接收開始");
    }

    void Update()
    {
        if (udp.Available > 0)
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 5005);

            //收到的資料是byte
            byte[] data = udp.Receive(ref remoteEP);

            //byte轉字串
            string message = Encoding.UTF8.GetString(data);

            Debug.Log("收到: " + message);
        }
    }

    void OnApplicationQuit()
    {
        udp.Close();
    }
}
