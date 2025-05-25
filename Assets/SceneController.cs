using UnityEngine;
using TMPro;
using System.Collections;

public class SceneController : MonoBehaviour
{
    [Header("연결할 스크립트")]
    public ResultLoader resultLoader;
    public FileTrajectoryVisualizer fileVisualizer;

    [Header("UI 요소 연결")]
    public TextMeshProUGUI statusText; // "생성 중" 및 카운트다운 표시용

    [Header("타이밍 설정")]
    public float showResultDuration = 5f;
    public int countdownSeconds = 5;

    void Start()
    {
        // 초기 상태 설정
        if (resultLoader == null || fileVisualizer == null || statusText == null)
        {
            Debug.LogError("[SceneController] 필요한 컴포넌트나 UI가 연결되지 않았습니다!");
            return;
        }

        resultLoader.SetActiveState(true); // ResultLoader 시작
        fileVisualizer.SetActiveState(false);
        statusText.gameObject.SetActive(false);

        // 메인 워크플로우 시작
        StartCoroutine(MainWorkflow());
    }

    IEnumerator MainWorkflow()
    {
        // 1. ResultLoader가 결과를 보여줄 때까지 기다림
        Debug.Log("[SceneController] ResultLoader 결과 대기 중...");
        yield return new WaitUntil(() => resultLoader.IsShowingResult);
        Debug.Log("[SceneController] ResultLoader 결과 확인! 5초 대기 시작.");

        // 2. ResultLoader 결과 5초간 보여주기
        yield return new WaitForSeconds(showResultDuration);

        // 3. ResultLoader 중지 및 숨기기
        Debug.Log("[SceneController] 5초 경과. 전환 시작.");
        resultLoader.StopPolling();
        resultLoader.SetActiveState(false);

        // 4. 전환 UI 및 카운트다운 시작
        statusText.gameObject.SetActive(true);
        for (int i = countdownSeconds; i > 0; i--)
        {
            statusText.text = $"새로운 궤적을 생성중 입니다 ({i}초...)";
            yield return new WaitForSeconds(1f);
        }

        // 5. 전환 UI 숨기기
        statusText.gameObject.SetActive(false);

        // 6. FileTrajectoryVisualizer 시작
        Debug.Log("[SceneController] 카운트다운 종료. FileVisualizer 시작.");
        fileVisualizer.SetActiveState(true);
    }
}