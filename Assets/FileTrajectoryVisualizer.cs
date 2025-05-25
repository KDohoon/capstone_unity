using UnityEngine;
using System.IO;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(LineRenderer))]
public class FileTrajectoryVisualizer : MonoBehaviour
{
    [Header("모니터링할 폴더")]
    public string folderPath = @"C:\Users\kdh03\Desktop\캡스톤\capstone_2024\generation_trajectory";

    [Header("렌더링 옵션")]
    public float scale = 1f;
    public float lineWidth = 0.02f;
    public float pollSec = 1f;

    string currentFile;
    System.DateTime currentStamp;
    struct Row { public float time; public Vector3 end; }

    private LineRenderer lr;
    private Coroutine watchCoroutine;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.widthMultiplier = lineWidth;
        lr.numCornerVertices = lr.numCapVertices = 8;
        SetGradient(1.0f); // 처음에는 불투명하게 설정
    }

    // 👈 그라데이션 설정 함수
    private void SetGradient(float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.red, 0.0f),
                new GradientColorKey(Color.red, 0.1f),
                new GradientColorKey(Color.white, 0.101f),
                new GradientColorKey(Color.white, 0.899f),
                new GradientColorKey(Color.yellow, 0.9f),
                new GradientColorKey(Color.yellow, 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(alpha, 0.0f),
                new GradientAlphaKey(alpha, 1.0f)
            }
        );
        lr.colorGradient = gradient;
    }

    // 👈 여기! SetTransparency 메소드
    public void SetTransparency(float alpha)
    {
        Debug.Log($"[FileVisualizer] 투명도를 {alpha}로 변경합니다.");
        SetGradient(Mathf.Clamp01(alpha));
    }

    public void SetActiveState(bool active)
    {
        if (lr != null) lr.enabled = active;

        if (active && watchCoroutine == null)
        {
            watchCoroutine = StartCoroutine(WatchFolder());
            Debug.Log("[FileVisualizer] Watching started.");
        }
        else if (!active && watchCoroutine != null)
        {
            StopCoroutine(watchCoroutine);
            watchCoroutine = null;
            if (lr != null) lr.positionCount = 0;
            Debug.Log("[FileVisualizer] Watching stopped.");
        }
    }

    IEnumerator WatchFolder() { /* ... 기존 코드와 동일 ... */
        while (true)
        {
            string latest = GetLatestFile(folderPath, "*.txt", out System.DateTime stamp);
            if (latest != null && (latest != currentFile || stamp != currentStamp))
            {
                currentFile = latest;
                currentStamp = stamp;
                DrawFile(latest);
            }
            yield return new WaitForSecondsRealtime(pollSec);
        }
    }
    string GetLatestFile(string dir, string pattern, out System.DateTime stamp) { /* ... 기존 코드와 동일 ... */
        stamp = System.DateTime.MinValue;
        if (!Directory.Exists(dir)) return null;
        var file = Directory.GetFiles(dir, pattern)
                            .OrderByDescending(File.GetLastWriteTime)
                            .FirstOrDefault();
        if (file != null) stamp = File.GetLastWriteTime(file);
        return file;
    }
    void DrawFile(string path) { /* ... 기존 코드와 동일 ... */
        List<Row> rows = ParseCsv(path);
        if (rows.Count == 0)
        {
            Debug.LogWarning($"[FTV] 유효 데이터 없음: {Path.GetFileName(path)}");
            lr.positionCount = 0;
            return;
        }
        rows.Sort((a, b) => a.time.CompareTo(b.time));
        lr.positionCount = rows.Count;
        for (int i = 0; i < rows.Count; ++i)
        {
            Vector3 p = rows[i].end;
            Vector3 unityPos = new Vector3(p.x, p.z, p.y) * scale;
            lr.SetPosition(i, unityPos);
        }
        Debug.Log($"[FTV] '{Path.GetFileName(path)}' 표시 ({rows.Count} pts)");
    }
    List<Row> ParseCsv(string path) { /* ... 기존 코드와 동일 ... */
        var lines = File.ReadAllLines(path);
        if (lines.Length < 2) return new List<Row>();
        bool F(string s, out float v) => float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        bool I(string s, out int v) => int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
        var rows = new List<Row>();
        bool header = true;
        foreach (var raw in lines)
        {
            if (header) { header = false; continue; }
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var col = raw.Split(',');
            if (col.Length == 5)
            {
                if (!I(col[1], out int seq)) continue;
                var ep = col[4].Split('/');
                if (ep.Length < 3 || !F(ep[0], out float x) || !F(ep[1], out float y) || !F(ep[2], out float z)) continue;
                rows.Add(new Row { time = seq, end = new Vector3(x, y, z) });
            }
            else if (col.Length >= 12)
            {
                if (col[0] == "s" || !I(col[1], out int seq) || !I(col[2], out int ts)) continue;
                var ep = col[6].Split('/');
                if (ep.Length < 3 || !F(ep[0], out float x) || !F(ep[1], out float y) || !F(ep[2], out float z)) continue;
                rows.Add(new Row { time = ts - seq - 1f, end = new Vector3(x, y, z) });
            }
        }
        return rows;
    }
}