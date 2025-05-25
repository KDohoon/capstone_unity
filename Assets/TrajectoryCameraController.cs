using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Camera))]
public class TrajectoryCameraController : MonoBehaviour
{
    [Header("타겟 설정")]
    public Transform trajectoryContainer; // 궤적들의 부모 오브젝트 (TrajectoryContainer)
    public float padding = 1.2f;        // 궤적 주변 여백 (1.2 = 20% 여백)

    [Header("카메라 설정")]
    public float minDistance = 5f;      // 최소 거리 (Perspective)
    public float minOrthographicSize = 2f; // 최소 크기 (Orthographic)

    [Header("움직임 설정")]
    public float positionSmoothTime = 0.5f;
    public float sizeSmoothTime = 0.5f;
    public float rotationSmoothTime = 0.5f;

    private Camera cam;
    private List<LineRenderer> lineRenderers = new List<LineRenderer>();

    private Vector3 targetPosition;
    private float targetSize; // Orthographic 크기 또는 Perspective 거리

    private Vector3 posVelocity;
    private float sizeVelocity;
    private Vector3 rotVelocity; // 회전을 위한 SmoothDamp (Euler 사용 시)

    void Start()
    {
        cam = GetComponent<Camera>();
        if (trajectoryContainer == null)
        {
            Debug.LogError("Trajectory Container가 설정되지 않았습니다!");
            enabled = false;
            return;
        }
        FetchLineRenderers();
    }

    // 궤적 오브젝트가 동적으로 추가/삭제될 경우 호출 필요
    void FetchLineRenderers()
    {
        lineRenderers.Clear();
        if (trajectoryContainer != null)
        {
            trajectoryContainer.GetComponentsInChildren<LineRenderer>(true, lineRenderers);
            Debug.Log($"{lineRenderers.Count}개의 LineRenderer를 찾았습니다.");
        }
    }

    void LateUpdate()
    {
        // 매 프레임마다 찾는 것은 비효율적일 수 있으나, 실시간 반영을 위해 임시로 사용
        // 또는 궤적 갱신 시점에 호출하도록 변경 가능
        FetchLineRenderers();

        Bounds bounds = CalculateWorldBounds();

        // 유효한 궤적이 없으면 움직이지 않음
        if (bounds.size == Vector3.zero)
        {
            return;
        }

        Vector3 boundsCenter = bounds.center;

        // 카메라의 목표 위치/크기/회전 계산
        if (cam.orthographic)
        {
            float requiredSizeX = bounds.size.x * 0.5f / cam.aspect;
            float requiredSizeY = bounds.size.y * 0.5f;
            targetSize = Mathf.Max(requiredSizeX, requiredSizeY) * padding;
            targetSize = Mathf.Max(targetSize, minOrthographicSize);

            // 카메라의 Z 위치는 유지하고 X, Y만 중앙으로 이동
            targetPosition = new Vector3(boundsCenter.x, boundsCenter.y, transform.position.z);

            // 카메라 크기와 위치 부드럽게 변경
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetSize, ref sizeVelocity, sizeSmoothTime);
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref posVelocity, positionSmoothTime);
            // Orthographic에서는 보통 회전을 고정합니다.
        }
        else // Perspective
        {
            float objectSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float fov = cam.fieldOfView * Mathf.Deg2Rad;
            float requiredDistance = (objectSize * 0.5f / Mathf.Tan(fov * 0.5f)) * padding;
            targetSize = Mathf.Max(requiredDistance, minDistance);

            // 궤적 중심에서 계산된 거리만큼 뒤로 물러난 위치가 목표
            // (주의: 카메라가 항상 뒤쪽(-Z)을 바라본다고 가정. 필요시 수정)
            Vector3 directionToTarget = (boundsCenter - transform.position).normalized;
            if (directionToTarget == Vector3.zero) directionToTarget = -transform.forward; // 만약 중앙에 있다면 뒤로

            targetPosition = boundsCenter - directionToTarget * targetSize;

            // 목표 방향 설정 (궤적 중심 바라보기)
            Quaternion targetRotation = Quaternion.LookRotation(boundsCenter - targetPosition);

            // 카메라 위치와 회전 부드럽게 변경
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref posVelocity, positionSmoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime / rotationSmoothTime);
        }
    }

    // 모든 LineRenderer 점들을 포함하는 World Space Bounds 계산
    Bounds CalculateWorldBounds()
    {
        Bounds bounds = new Bounds();
        bool firstPoint = true;

        foreach (var lr in lineRenderers)
        {
            if (lr == null || !lr.gameObject.activeInHierarchy || !lr.enabled || lr.positionCount == 0) continue;

            Vector3[] positions = new Vector3[lr.positionCount];
            lr.GetPositions(positions);

            Transform lrTransform = lr.transform; // LineRenderer의 Transform 캐싱

            for (int i = 0; i < lr.positionCount; i++)
            {
                // LineRenderer가 Local Space를 사용하면 World로 변환
                // (이전 답변에서 Local Space를 사용하도록 변경했으므로 변환 필요)
                Vector3 worldPos = lr.useWorldSpace ? positions[i] : lrTransform.TransformPoint(positions[i]);

                if (firstPoint)
                {
                    bounds = new Bounds(worldPos, Vector3.zero);
                    firstPoint = false;
                }
                else
                {
                    bounds.Encapsulate(worldPos);
                }
            }
        }
        return bounds;
    }
}