using UnityEngine;

/// <summary>
/// 北極熊寵物的簡易 AI：在指定區域內隨機走動、偶爾發呆與睡覺。
/// 整合互動/拖拽邏輯：當寵物被拖拽、進行互動（吃、洗澡）時不自動移動。
/// 動畫狀態：Idle、Walk、Sleep、Stare（可於 Inspector 指定對應動畫名）。
/// 設計重點：
/// - 盡量避免每幀開銷，僅在需要時更新方向與目標；
/// - 使用協程/計時器管理狀態停留時間；
/// - 使用 SpriteRenderer.flipX 控制朝向；
/// - 與 PetMoveArea 協作限制移動範圍並繪製 Gizmos。
/// </summary>
[RequireComponent(typeof(Animator))]
public class PetAIController : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private Animator animator;
	[SerializeField] private SpriteRenderer spriteRenderer;
	[SerializeField] private PetMoveArea moveArea;
	[SerializeField] private PetInteractionState interactionState;
	[SerializeField] private PetDragController dragController;

	[Header("Animation Names")]
	[SerializeField] private string idleStateName = "Idle";
	[SerializeField] private string walkStateName = "Walk_bear";
	[SerializeField] private string sleepStateName = "Sleep";
	[SerializeField] private string stareStateName = "Stare";

	[Header("Movement")]
	[Tooltip("每秒移動速度（世界單位/秒）")]
	[SerializeField] private float moveSpeed = 1.2f;
	[Tooltip("距離目標點小於此值視為抵達，避免抖動")]
	[SerializeField] private float arriveDistance = 0.05f;
	[Tooltip("Z 軸固定值（確保排序層級），若不需要可維持 0")]
	[SerializeField] private float fixedWorldZ = 0f;

	[Header("State Durations (Seconds)")]
	[Tooltip("發呆時長範圍（含端點）")]
	[SerializeField] private Vector2 stareDurationRange = new Vector2(1.5f, 4f);
	[Tooltip("睡覺時長範圍（含端點）")]
	[SerializeField] private Vector2 sleepDurationRange = new Vector2(4f, 9f);
	[Tooltip("連續行走時長範圍（含端點）")]
	[SerializeField] private Vector2 walkDurationRange = new Vector2(2f, 6f);

	private enum AiState { Idle, Walk, Stare, Sleep }
	private AiState currentState = AiState.Idle;
	private float stateTimer;
	private Vector3 walkTarget;

	/// <summary>
	/// 快取依賴並進入初始狀態
	/// </summary>
	private void Awake()
	{
		if (animator == null) animator = GetComponentInChildren<Animator>();
		if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
		if (interactionState == null) interactionState = GetComponent<PetInteractionState>();
		if (dragController == null) dragController = GetComponent<PetDragController>();
		if (moveArea == null) moveArea = FindObjectOfType<PetMoveArea>();
	}

	private void OnEnable()
	{
		SwitchToIdle();
	}

	private void Update()
	{
		// 若正在互動或被拖拽，暫停 AI（保持當前動畫）
		if ((interactionState != null && interactionState.IsInteracting) ||
			(dragController != null && dragController.IsDragging))
		{
			return;
		}

		// 狀態機計時與行為
		switch (currentState)
		{
			case AiState.Walk:
				TickWalk();
				break;
			case AiState.Stare:
				TickCountdownThenRandomNext();
				break;
			case AiState.Sleep:
				TickCountdownThenRandomNext();
				break;
			case AiState.Idle:
				TickCountdownThenRandomNext();
				break;
		}
	}

	/// <summary>
	/// 行走邏輯：朝目標點移動；抵達或超時後換下一個狀態
	/// </summary>
	private void TickWalk()
	{
		Vector3 pos = transform.position;
		Vector3 target = walkTarget;
		target.z = fixedWorldZ;
		if (moveArea != null)
		{
			target = moveArea.ClampToArea(target);
		}

		Vector3 next = Vector3.MoveTowards(pos, target, moveSpeed * Time.deltaTime);
		transform.position = new Vector3(next.x, next.y, fixedWorldZ);

		// Flip 朝向
		if (spriteRenderer != null)
		{
			float dx = target.x - pos.x;
			if (Mathf.Abs(dx) > 0.001f)
			{
				spriteRenderer.flipX = dx < 0f;
			}
		}

		// 抵達目標
		if (Vector2.Distance(new Vector2(transform.position.x, transform.position.y), new Vector2(target.x, target.y)) <= arriveDistance)
		{
			// 在行走後隨機進入 Idle/發呆/睡覺
			SwitchToRandomRestState();
		}
		else
		{
			stateTimer -= Time.deltaTime;
			if (stateTimer <= 0f)
			{
				// 行走逾時也切換
				SwitchToRandomRestState();
			}
		}
	}

	private void TickCountdownThenRandomNext()
	{
		stateTimer -= Time.deltaTime;
		if (stateTimer <= 0f)
		{
			SwitchToWalk();
		}
	}

	private void SwitchToIdle()
	{
		currentState = AiState.Idle;
		stateTimer = Random.Range(stareDurationRange.x, stareDurationRange.y);
		PlayAnimationSafe(idleStateName);
	}

	private void SwitchToStare()
	{
		currentState = AiState.Stare;
		stateTimer = Random.Range(stareDurationRange.x, stareDurationRange.y);
		PlayAnimationSafe(stareStateName);
	}

	private void SwitchToSleep()
	{
		currentState = AiState.Sleep;
		stateTimer = Random.Range(sleepDurationRange.x, sleepDurationRange.y);
		PlayAnimationSafe(sleepStateName);
	}

	private void SwitchToWalk()
	{
		currentState = AiState.Walk;
		stateTimer = Random.Range(walkDurationRange.x, walkDurationRange.y);
		PickNewWalkTarget();
		PlayAnimationSafe(walkStateName);
	}

	private void SwitchToRandomRestState()
	{
		float r = Random.value;
		if (r < 0.2f)
		{
			SwitchToSleep();
		}
		else if (r < 0.55f)
		{
			SwitchToStare();
		}
		else
		{
			SwitchToIdle();
		}
	}

	private void PickNewWalkTarget()
	{
		if (moveArea != null)
		{
			walkTarget = moveArea.GetRandomWorldPoint(fixedWorldZ);
		}
		else
		{
			// 無區域時，以當前位置附近小半徑挑點
			Vector2 rnd = Random.insideUnitCircle * 1.5f;
			walkTarget = new Vector3(transform.position.x + rnd.x, transform.position.y + rnd.y, fixedWorldZ);
		}
	}

	private void PlayAnimationSafe(string stateName)
	{
		if (animator == null) return;
		if (string.IsNullOrEmpty(stateName)) return;
		animator.Play(stateName);
	}
}


