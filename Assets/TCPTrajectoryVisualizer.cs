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
    // 👈 FileTrajectoryVisualizer 참조 추가
    public FileTrajectoryVisualizer fileVisualizer;
    public float targetAlpha = 0.3f; // 👈 반투명도 설정 (0.0 ~ 1.0)

    private TcpListener tcpListener;
    private Thread listenThread;

    private Queue<Vector3> positionQueue = new Queue<Vector3>();
    private object queueLock = new object();

    private List<Vector3> trajectoryPoints = new List<Vector3>();
    private LineRenderer lineRenderer;

    private bool skipFirstData = true;
    // 👈 투명도 설정을 한 번만 하도록 플래그 추가
    private bool transparencySet = false;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        // 👈 LineRenderer 설정 (너비, 색상 등)
        lineRenderer.startWidth = 0.02f; // FileVisualizer와 비슷하게 조절 (또는 원하는 값)
        lineRenderer.endWidth = 0.02f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.black; // 시작 색상
        lineRenderer.endColor = Color.black;   // 끝 색상
        lineRenderer.numCornerVertices = lineRenderer.numCapVertices = 8; // 모서리 둥글게

        listenThread = new Thread(new ThreadStart(ListenForClients));
        listenThread.IsBackground = true;
        listenThread.Start();
        Debug.Log("TCP Trajectory Visualizer started on port " + port);
    }

    void ListenForClients() { /* ... 기존 코드와 동일 ... */
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

    void HandleClientComm(object clientObj) { /* ... 기존 코드와 동일 ... */
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
                string wrappedData = "{\"records\":" + data + "}";
                try
                {
                    TrajectoryRecords recordsWrapper = JsonUtility.FromJson<TrajectoryRecords>(wrappedData);
                    if (recordsWrapper != null && recordsWrapper.records != null)
                    {
                        foreach (var record in recordsWrapper.records)
                        {
                            EnqueuePosition(new Vector3(record.x_end, record.z_end, record.y_end));
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

    void EnqueuePosition(Vector3 pos) { /* ... 기존 코드와 동일 ... */
         lock (queueLock)
        {
            positionQueue.Enqueue(pos);
        }
     }

    void Update()
    {
        bool pointAdded = false; // 👈 이번 업데이트에서 점이 추가되었는지 확인
        lock (queueLock)
        {
            while (positionQueue.Count > 0)
            {
                Vector3 pos = positionQueue.Dequeue();
                if (skipFirstData)
                {
                    skipFirstData = false;
                    continue;
                }
                trajectoryPoints.Add(pos);
                pointAdded = true; // 👈 점 추가됨!
            }
        }

        if (trajectoryPoints.Count > 0)
        {
            lineRenderer.positionCount = trajectoryPoints.Count;
            lineRenderer.SetPositions(trajectoryPoints.ToArray());

            // 👈 점이 추가되었고, 아직 투명도 설정을 안 했다면?
            if (pointAdded && !transparencySet)
            {
                if (fileVisualizer != null)
                {
                    // 👈 FileVisualizer의 투명도 설정 호출!
                    fileVisualizer.SetTransparency(targetAlpha);
                    transparencySet = true; // 👈 이제 설정 완료!
                }
                else
                {
                    Debug.LogWarning("[TCPVisualizer] FileVisualizer가 연결되지 않았습니다!");
                    transparencySet = true; // 경고 후 다시 시도하지 않도록 설정
                }
            }
        }
    }

    [Serializable] public class TrajectoryRecord { /* ... 기존 코드와 동일 ... */
        public float x_end;
        public float y_end;
        public float z_end;
     }
    [Serializable] public class TrajectoryRecords { /* ... 기존 코드와 동일 ... */
        public List<TrajectoryRecord> records;
     }

    void OnApplicationQuit() { /* ... 기존 코드와 동일 ... */
        if (tcpListener != null)
        {
            tcpListener.Stop();
        }
     }
}