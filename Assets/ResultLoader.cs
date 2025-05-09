using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

// ──────────────────────────────────────────────
//   ResultLoader.cs
//   • trajectory_result.json → selected_file 읽기
//   • txt 파싱 → LineRenderer로 표시
// ──────────────────────────────────────────────
[RequireComponent(typeof(LineRenderer))]
public class ResultLoader : MonoBehaviour
{
    // ▒▒ JSON 파일의 “절대 경로” ▒▒
    //   (인스펙터에서 그대로 붙여넣어도 되고, 코드에 상수로 둬도 됩니다)
    [Header("외부 JSON 경로")]
    public string resultJsonAbsPath =  @"C:\Users\kdh03\Desktop\캡스톤\capstone_2024\data\trajectory_result.json";

    [Header("Unity 내부 txt 위치")]
    public string trajFolderInAssets = "all_data";     // txt 보관 폴더
    public float  scale      = 1f;
    public float  lineWidth  = 0.02f;
    public float  pollSec    = 1f;  
    public int   subdiv     = 4;                                // 체크 주기

    string lastTimestamp;

    void Start()
    {
        StartCoroutine(PollJson());
    }

    IEnumerator PollJson()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(pollSec);
            if (!File.Exists(resultJsonAbsPath)) continue;

            string json;
            try   { json = File.ReadAllText(resultJsonAbsPath); }
            catch { continue; }

            var node = JsonUtility.FromJson<JsonNode>(json);
            if (node == null || node.timestamp == lastTimestamp) continue;
            lastTimestamp = node.timestamp;

            Debug.Log($"[ResultLoader] 새 JSON 감지 → {node.selected_file}");
            TryDraw(node.selected_file);
        }
    }

    /*────────────────── txt 파싱 & 그리기 ───────*/
    void TryDraw(string fileName)
    {
        string assetFolder = Path.Combine(Application.dataPath, trajFolderInAssets);
        string txtPath     = Path.Combine(assetFolder, fileName);
        if (!File.Exists(txtPath))
        {
            Debug.LogWarning($"[ResultLoader] '{fileName}'를 {assetFolder}에서 찾을 수 없습니다.");
            return;
        }

        /* 안전 숫자 파서 */
        bool F(string s, out float v) =>
             float.TryParse(s.Trim(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out v);

        var xs = new List<float>(); var ys = new List<float>();
        var zs = new List<float>(); var ts = new List<float>();

        bool header = true;
        foreach (var raw in File.ReadLines(txtPath))
        {
            if (header) { header = false; continue; }
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var col = raw.Split(',');
            if (col.Length < 12) continue;
            if (col[0] == "s") continue;

            if (!F(col[1], out float seq) || !F(col[2], out float tstamp)) continue;

            var ep = col[6].Split('/');
            if (ep.Length < 3 ||
                !F(ep[0], out float x) ||
                !F(ep[1], out float y) ||
                !F(ep[2], out float z)) continue;

            xs.Add(x); ys.Add(y); zs.Add(z);
            ts.Add(tstamp - seq - 1f);
        }

        if (xs.Count == 0)
        {
            Debug.LogWarning($"[ResultLoader] '{fileName}' 유효 데이터가 없습니다.");
            return;
        }

        /* time 기준 정렬 */
        int n = xs.Count;
        int[] idx = new int[n];
        for (int i = 0; i < n; ++i) idx[i] = i;
        System.Array.Sort(idx, (a, b) => ts[a].CompareTo(ts[b]));

        var pts = new Vector3[n];
        for (int k = 0; k < n; ++k)
        {
            int i = idx[k];
            /* 축 변환:  수학계(x,y,z) → Unity(x,z,y)  */
            pts[k] = new Vector3(xs[i],  /* x는 그대로  */
                                zs[i],  /*   z → Unity‑y(위) */
                                ys[i])  /*   y → Unity‑z(앞) */
                    * scale;
        }

        /*────────── Catmull‑Rom 스무딩 ─────────*/
        pts = CatmullRomSmooth(pts, Mathf.Max(1, subdiv));

        /*────────── LineRenderer ─────────────*/
        var lr = GetComponent<LineRenderer>();
        lr.numCornerVertices = lr.numCapVertices = 8;   // 둥근 모서리
        lr.material          = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = Color.black;
        lr.widthMultiplier = lineWidth;
        lr.positionCount   = pts.Length;
        lr.SetPositions(pts);

        Debug.Log($"[ResultLoader] '{fileName}' 그리기 완료 ({pts.Length} pts, smoothed)");
    }

    /*────────────────── Catmull‑Rom 유틸 ───────*/
    Vector3[] CatmullRomSmooth(Vector3[] src, int div)
    {
        if (src.Length < 4 || div < 2) return src;
        var dst = new List<Vector3>();
        for (int i = 0; i < src.Length - 3; i++)
        {
            Vector3 p0 = src[i],     p1 = src[i + 1];
            Vector3 p2 = src[i + 2], p3 = src[i + 3];
            for (int j = 0; j < div; j++)
            {
                float t = j / (float)div;
                float t2 = t * t, t3 = t2 * t;
                Vector3 q = 0.5f * ((2f * p1) +
                           (-p0 + p2) * t +
                           (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                           (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
                dst.Add(q);
            }
        }
        dst.Add(src[^2]); dst.Add(src[^1]);
        return dst.ToArray();
    }

    /*──────── JSON 매핑용 구조체 ────────*/
    [System.Serializable]
    class JsonNode
    {
        public string selected_file;
        public string target_file;
        public float  score;
        public int    rank;
        public string timestamp;
    }
}