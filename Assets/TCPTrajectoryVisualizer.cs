using UnityEngine;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;

public class TCPTrajectoryVisualizer : MonoBehaviour
{
    public int port = 5005;
    private TcpListener tcpListener;
    private Thread listenThread;

    private Queue<Vector3> positionQueue = new Queue<Vector3>();
    private object queueLock = new object();

    private List<Vector3> trajectoryPoints = new List<Vector3>();
    private LineRenderer lineRenderer;

    // 첫 번째 데이터는 건너뛰기 위한 플래그
    private bool skipFirstData = true;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        lineRenderer.startWidth = 2f;
        lineRenderer.endWidth = 2f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.material.color = Color.black;

        listenThread = new Thread(new ThreadStart(ListenForClients));
        listenThread.IsBackground = true;
        listenThread.Start();
        Debug.Log("TCP Trajectory Visualizer started on port " + port);
    }

    void ListenForClients()
    {
        tcpListener = new TcpListener(IPAddress.Any, port);
        tcpListener.Start();
        try
        {
            while (true)
            {
                TcpClient client = tcpListener.AcceptTcpClient();
                Debug.Log("Client connected.");
                Thread clientThread = new Thread(new ParameterizedThreadStart(HandleClientComm));
                clientThread.IsBackground = true;
                clientThread.Start(client);
            }
        }
        catch (SocketException se)
        {
            Debug.Log("Socket exception: " + se.Message);
        }
    }

    void HandleClientComm(object clientObj)
    {
        TcpClient client = (TcpClient)clientObj;
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[8192];
        int bytesRead;
        try
        {
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Debug.Log("Received data: " + data);

                // JSON 배열을 파싱하기 위해 객체로 Wrapping
                string wrappedData = "{\"records\":" + data + "}";

                try
                {
                    TrajectoryRecords recordsWrapper = JsonUtility.FromJson<TrajectoryRecords>(wrappedData);

                    if (recordsWrapper != null && recordsWrapper.records != null)
                    {
                        foreach (var record in recordsWrapper.records)
                            {
                                /* ★ 축 변환 한-줄만 수정 ★
                                (x ,y ,z)  →  (x ,z ,y)  -- z를 ↑ 로 사용 */
                                EnqueuePosition(new Vector3(record.x_end,   // x 그대로
                                                            record.z_end, // z → Unity-y(위)
                                                            record.y_end  // y → Unity-z(앞)
                                                            ));
                            }

                    }
                    else
                    {
                        Debug.LogWarning("파싱 결과가 null입니다.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log("JSON parse error: " + ex.Message);
                }
            }
        }
        catch (Exception e)
        {
            Debug.Log("Error in client communication: " + e.Message);
        }
        client.Close();
        Debug.Log("Client disconnected.");
    }

    void EnqueuePosition(Vector3 pos)
    {
        lock (queueLock)
        {
            positionQueue.Enqueue(pos);
        }
    }

    void Update()
    {
        lock (queueLock)
        {
            while (positionQueue.Count > 0)
            {
                Vector3 pos = positionQueue.Dequeue();
                // 첫 번째 데이터는 건너뜁니다.
                if (skipFirstData)
                {
                    skipFirstData = false;
                    continue;
                }
                trajectoryPoints.Add(pos);
            }
        }

        if (trajectoryPoints.Count > 0)
        {
            lineRenderer.positionCount = trajectoryPoints.Count;
            lineRenderer.SetPositions(trajectoryPoints.ToArray());
        }
    }

    [Serializable]
    public class TrajectoryRecord
    {
        public float x_end;
        public float y_end;
        public float z_end;
    }

    [Serializable]
    public class TrajectoryRecords
    {
        public List<TrajectoryRecord> records;
    }

    void OnApplicationQuit()
    {
        if (tcpListener != null)
        {
            tcpListener.Stop();
        }
    }
}
