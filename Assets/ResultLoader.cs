using UnityEngine;
using TMPro; // TextMeshPro 네임스페이스
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

[RequireComponent(typeof(LineRenderer))]
public class ResultLoader : MonoBehaviour
{
    [Header("외부 JSON 경로")]
    public string resultJsonAbsPath = @"C:\Users\kdh03\Desktop\캡스톤\capstone_2024\data\trajectory_result.json";

    [Header("Unity 내부 txt 위치")]
    public string trajFolderInAssets = "all_data";
    public float scale = 1f;
    public float lineWidth = 0.02f;
    public float pollSec = 1f;
    public int subdiv = 4;

    [Header("UI 요소 연결")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI rankText;

    // 👈 여기! 추가/수정된 부분
    public bool IsShowingResult { get; private set; } = false;
    private LineRenderer lr;
    private Coroutine pollCoroutine;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
    }

    void Start()
    {
        pollCoroutine = StartCoroutine(PollJson());
        SetActiveState(true); // 시작 시 기본 활성화 (SceneController가 제어할 수도 있음)
    }

    // 👈 여기! 추가된 메소드
    public void StopPolling()
    {
        if (pollCoroutine != null)
        {
            StopCoroutine(pollCoroutine);
            pollCoroutine = null;
            Debug.Log("[ResultLoader] Polling stopped.");
        }
    }

    // 👈 여기! 추가된 메소드
    public void SetActiveState(bool active)
    {
        if (lr != null) lr.enabled = active;
        if (scoreText != null) scoreText.gameObject.SetActive(active);
        if (rankText != null) rankText.gameObject.SetActive(active);
        if (!active) IsShowingResult = false; // 비활성화 시 false로 설정
        Debug.Log($"[ResultLoader] SetActiveState called: {active}");
    }


    IEnumerator PollJson()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(pollSec);
            if (!File.Exists(resultJsonAbsPath)) continue;

            string json;
            try { json = File.ReadAllText(resultJsonAbsPath); }
            catch { continue; }

            var node = JsonUtility.FromJson<JsonNode>(json);
            if (node == null) continue;

            Debug.Log($"[ResultLoader] JSON 감지 → {node.selected_file}");

            UpdateUI(node.score, node.rank);
            TryDraw(node.selected_file);
        }
    }

    void UpdateUI(float score, int rank)
    {
        if (scoreText != null) scoreText.text = $"점수: {score:F1}";
        if (rankText != null) rankText.text = $"등급: {rank}등급";
    }

    void TryDraw(string fileName)
    {
        string assetFolder = Path.Combine(Application.dataPath, trajFolderInAssets);
        string txtPath = Path.Combine(assetFolder, fileName);
        if (!File.Exists(txtPath))
        {
            Debug.LogWarning($"[ResultLoader] '{fileName}'를 {assetFolder}에서 찾을 수 없습니다.");
            // IsShowingResult = false; // 실패 시 false (계속 시도할 수 있으므로 여기서 false로 단정하지 않을 수 있음)
            return;
        }

        bool F(string s, out float v) => float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        var xs = new List<float>(); var ys = new List<float>();
        var zs = new List<float>(); var ts = new List<float>();

        bool header = true;
        foreach (var raw in File.ReadLines(txtPath))
        {
            if (header) { header = false; continue; }
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var col = raw.Split(',');
            if (col.Length < 12 || col[0] == "s") continue;
            if (!F(col[1], out float seq) || !F(col[2], out float tstamp)) continue;
            var ep = col[6].Split('/');
            if (ep.Length < 3 || !F(ep[0], out float x) || !F(ep[1], out float y) || !F(ep[2], out float z)) continue;
            xs.Add(x); ys.Add(y); zs.Add(z); ts.Add(tstamp - seq - 1f);
        }

        if (xs.Count == 0)
        {
            Debug.LogWarning($"[ResultLoader] '{fileName}' 유효 데이터가 없습니다.");
            // IsShowingResult = false;
            return;
        }

        int n = xs.Count; int[] idx = new int[n];
        for (int i = 0; i < n; ++i) idx[i] = i;
        System.Array.Sort(idx, (a, b) => ts[a].CompareTo(ts[b]));

        var pts = new Vector3[n];
        for (int k = 0; k < n; ++k)
        {
            int i = idx[k];
            pts[k] = new Vector3(xs[i], zs[i], ys[i]) * scale;
        }

        pts = CatmullRomSmooth(pts, Mathf.Max(1, subdiv));

        lr.numCornerVertices = lr.numCapVertices = 8;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = Color.black;
        lr.widthMultiplier = lineWidth;
        lr.positionCount = pts.Length;
        lr.SetPositions(pts);

        Debug.Log($"[ResultLoader] '{fileName}' 그리기 완료 ({pts.Length} pts, smoothed)");
        IsShowingResult = true; // 👈 성공 시 true로 설정!
    }

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

    [System.Serializable] class JsonNode {
        public string selected_file;
        public string target_file;
        public float  score;
        public int    rank;
        public string timestamp;
    }
}