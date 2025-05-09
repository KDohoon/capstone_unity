using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraAutoFrame : MonoBehaviour
{
    [Header("라인/메시 렌더러")]
    public Renderer targetRenderer;                 // LineRenderer 포함

    [Header("카메라가 볼 방향(센터 → 카메라)")]
    public Vector3 viewDirection = new(0, 1, -2);   // 위·뒤 방향

    public float margin = 1.1f;                     // 여백 비율 (>1)

    [Header("부드러운 추적")]
    public float moveSmooth = 0.5f;                 // 위치 보간
    public float rotSmooth  = 0.3f;                 // 회전 보간

    Camera cam;
    Vector3 velPos;

    void Awake() => cam = GetComponent<Camera>();

    void LateUpdate()
    {
        if (!targetRenderer || !targetRenderer.enabled) return;

        Bounds b = targetRenderer.bounds;
        if (b.size == Vector3.zero) return;         // 아직 점 없음

        Vector3 center  = b.center;
        float   radius  = b.extents.magnitude * margin;

        Vector3 dir     = viewDirection.normalized;
        float   dist    = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

        // *** 부호 수정: center + dir * dist ***
        Vector3 desiredPos = center + dir * dist;

        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref velPos, moveSmooth);

        Quaternion desiredRot = Quaternion.LookRotation(center - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, Time.deltaTime / rotSmooth);
    }
}
