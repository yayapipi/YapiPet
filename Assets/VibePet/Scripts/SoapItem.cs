using UnityEngine;

/// <summary>
/// 肥皂道具：
/// - 可拖拽；
/// - 拖到寵物上時進入搓澡（播放 Bath），離開或放手即結束；
/// - 搓澡期間每秒隨機在寵物周圍生成泡泡；
/// - 搓澡期間鎖定寵物移動（由 PetInteractionState 處理）。
/// 使用：掛到 Soap 預置體，需有 Collider2D（Is Trigger），建議加 Rigidbody2D(Kinematic)。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SoapItem : DraggableItem
{
	[Header("Pet Interaction")]
	[SerializeField] private PetInteractionState pet;
	[SerializeField] private string bathStateName = "Bath";

	[Header("Bubbles")]
	[Tooltip("泡泡預置體（帶有 SpriteRenderer 與 BubbleEffect 組件）")]
	[SerializeField] private GameObject bubblePrefab;
	[Tooltip("泡泡節點父物件（可選）")]
	[SerializeField] private Transform bubbleParent;
	[Tooltip("期望每秒生成的泡泡數")]
	[SerializeField] private float bubblesPerSecond = 6f;
	[Tooltip("泡泡生成範圍半徑（以寵物中心為圓心）")]
	[SerializeField] private float bubbleRadius = 1.2f;
	[Tooltip("最大同時存在泡泡數（池大小）")]
	[SerializeField] private int bubblePoolSize = 16;

	private bool isBathing;
	private GameObject[] bubblePool;
	private int bubbleIndex;
	private float bubbleAccumulator;

	protected override void Awake()
	{
		base.Awake();
		var col = GetComponent<Collider2D>();
		if (col != null) col.isTrigger = true;
		if (pet == null) pet = FindObjectOfType<PetInteractionState>();
		InitBubblePool();
	}

	private void InitBubblePool()
	{
		if (bubblePrefab == null || bubblePoolSize <= 0)
		{
			bubblePool = null;
			return;
		}
		bubblePool = new GameObject[bubblePoolSize];
		for (int i = 0; i < bubblePoolSize; i++)
		{
			bubblePool[i] = Object.Instantiate(bubblePrefab, bubbleParent == null ? transform : bubbleParent);
			bubblePool[i].SetActive(false);
		}
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (pet == null) return;
		if (!other.transform.IsChildOf(pet.transform) && other.transform != pet.transform) return;
		StartBath();
	}

	private void OnTriggerExit2D(Collider2D other)
	{
		if (pet == null) return;
		if (!other.transform.IsChildOf(pet.transform) && other.transform != pet.transform) return;
		StopBath();
	}

	protected override void OnDragging()
	{
		base.OnDragging();
		if (!isBathing) return;
		SpawnBubbles(Time.deltaTime);
	}

	protected override void OnDragEnd()
	{
		base.OnDragEnd();
		StopBath();
	}

	private void StartBath()
	{
		if (isBathing) return;
		isBathing = true;
		bubbleAccumulator = 0f;
		if (pet != null)
		{
			// 進入互動（Bath），音效可在 Animator 或外部管理
			pet.BeginInteraction(bathStateName, null, 1f);
		}
	}

	private void StopBath()
	{
		if (!isBathing) return;
		isBathing = false;
		if (pet != null)
		{
			pet.EndInteraction();
		}
	}

	private void SpawnBubbles(float deltaTime)
	{
		if (bubblePool == null || bubblePrefab == null) return;
		if (bubblesPerSecond <= 0f) return;
		bubbleAccumulator += bubblesPerSecond * deltaTime;
		int count = Mathf.FloorToInt(bubbleAccumulator);
		if (count <= 0) return;
		bubbleAccumulator -= count;

		for (int i = 0; i < count; i++)
		{
			var go = NextBubble();
			if (go == null) return;
			Vector2 rnd = Random.insideUnitCircle * bubbleRadius;
			Vector3 center = pet != null ? pet.transform.position : transform.position;
			go.transform.position = new Vector3(center.x + rnd.x, center.y + rnd.y, center.z);
			go.SetActive(true);
			var eff = go.GetComponent<BubbleEffect>();
			if (eff != null) eff.Play();
		}
	}

	private GameObject NextBubble()
	{
		if (bubblePool == null || bubblePool.Length == 0) return null;
		for (int n = 0; n < bubblePool.Length; n++)
		{
			bubbleIndex = (bubbleIndex + 1) % bubblePool.Length;
			if (!bubblePool[bubbleIndex].activeSelf) return bubblePool[bubbleIndex];
		}
		bubbleIndex = (bubbleIndex + 1) % bubblePool.Length;
		return bubblePool[bubbleIndex];
	}
}


