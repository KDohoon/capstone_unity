using UnityEngine;
using System.IO;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/*─────────────────────────────────────────────────────────────
 *  FileTrajectoryVisualizer.cs
 *  • 지정된 폴더에서 가장 “최근 저장”된 txt를 찾아 궤적 표시
 *  • 새 txt가 생성·갱신되면 실시간으로 다시 그려 줌
 *  • 5 컬럼( r,sequence,timestamp,deg,endpoint )
 *    & 12 컬럼( 구형 포맷 ) 둘 다 자동 인식
 *────────────────────────────────────────────────────────────*/
[RequireComponent(typeof(LineRenderer))]
public class FileTrajectoryVisualizer : MonoBehaviour
{
    [Header("모니터링할 폴더")]
    public string folderPath =
        @"C:\Users\kdh03\Desktop\캡스톤\capstone_2024\generation_trajectory";

    [Header("렌더링 옵션")]
    public float  scale     = 1f;
    public float  lineWidth = 0.02f;
    public float  pollSec   = 1f;     // 새 파일 체크 주기(초)

    /*──────── 내부 상태 ───────*/
    string currentFile;
    System.DateTime currentStamp;

    struct Row { public float time; public Vector3 end; }

    /*───────────────── Unity 루틴 ─────────────────*/
    void Start()
    {
        var lr = GetComponent<LineRenderer>();
        lr.material        = new Material(Shader.Find("Sprites/Default"));
        lr.startColor      = lr.endColor = Color.white; // 흰 선
        lr.widthMultiplier = lineWidth;
        lr.numCornerVertices = lr.numCapVertices = 8;

        StartCoroutine(WatchFolder());
    }

    /*──────────── 폴더 감시 코루틴 ────────────*/
    IEnumerator WatchFolder()
    {
        while (true)
        {
            string latest = GetLatestFile(folderPath, "*.txt",
                                          out System.DateTime stamp);
            if (latest != null &&
               (latest != currentFile || stamp != currentStamp))
            {
                currentFile  = latest;
                currentStamp = stamp;
                DrawFile(latest);
            }
            yield return new WaitForSecondsRealtime(pollSec);
        }
    }

    /*──────────── 최신 파일 찾기 ─────────────*/
    string GetLatestFile(string dir, string pattern,
                         out System.DateTime stamp)
    {
        stamp = System.DateTime.MinValue;
        if (!Directory.Exists(dir)) return null;

        var file = Directory.GetFiles(dir, pattern)
                            .OrderByDescending(File.GetLastWriteTime)
                            .FirstOrDefault();
        if (file != null) stamp = File.GetLastWriteTime(file);
        return file;
    }

    /*──────────── txt 읽어 그리기 ────────────*/
    void DrawFile(string path)
    {
        List<Row> rows = ParseCsv(path);
        if (rows.Count == 0)
        {
            Debug.LogWarning($"[FTV] 유효 데이터 없음: {Path.GetFileName(path)}");
            return;
        }

        rows.Sort((a, b) => a.time.CompareTo(b.time));

        var lr = GetComponent<LineRenderer>();
        lr.positionCount = rows.Count;

        /* ★★ 축 변환 한‑줄만 변경 ★★
        (x ,y ,z)  →  (x ,z ,y)   :   z를 ↑,  y를 앞쪽으로 */
        for (int i = 0; i < rows.Count; ++i)
        {
            Vector3 p = rows[i].end;
            lr.SetPosition(i, new Vector3(p.x, p.z, p.y) * scale);
        }

        Debug.Log($"[FTV] '{Path.GetFileName(path)}' 표시 ({rows.Count} pts)");
    }

    /*──────────── CSV 파싱 (5열 & 12열) ────────*/
    List<Row> ParseCsv(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 2) return new List<Row>();

        bool F(string s, out float v) => float.TryParse(
                s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        bool I(string s, out int v)   =>   int.TryParse(
                s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v);

        var rows = new List<Row>();
        bool header = true;

        foreach (var raw in lines)
        {
            if (header) { header = false; continue; }
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var col = raw.Split(',');
            if (col.Length == 5)
            {   /* 신형 포맷: r,sequence,timestamp,deg,endpoint */
                if (!I(col[1], out int seq)) continue;
                var ep = col[4].Split('/');
                if (ep.Length < 3 ||
                    !F(ep[0], out float x) ||
                    !F(ep[1], out float y) ||
                    !F(ep[2], out float z)) continue;
                rows.Add(new Row { time = seq, end = new Vector3(x, y, z) });
            }
            else if (col.Length >= 12)
            {   /* 구형 포맷 */
                if (col[0] == "s") continue;
                if (!I(col[1], out int seq) || !I(col[2], out int ts)) continue;
                var ep = col[6].Split('/');
                if (ep.Length < 3 ||
                    !F(ep[0], out float x) ||
                    !F(ep[1], out float y) ||
                    !F(ep[2], out float z)) continue;
                float time = ts - seq - 1f;
                rows.Add(new Row { time = time, end = new Vector3(x, y, z) });
            }
        }
        return rows;
    }
}
