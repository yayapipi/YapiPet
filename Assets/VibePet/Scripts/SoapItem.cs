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
	[Tooltip("搓澡音效（播放在寵物 AudioSource 上）")]
	[SerializeField] private AudioClip bathSfx;
	[SerializeField] [Range(0f, 1f)] private float bathSfxVolume = 1f;

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

	[Header("Usage Limit")]
	[Tooltip("肥皂可搓澡的最大次數（用完即銷毀）")]
	[SerializeField] private int maxScrubCount = 10;
	[Tooltip("每累積多少拖拽距離算 1 次搓澡（世界座標單位）")]
	[SerializeField] private float scrubDistancePerCount = 0.25f;
	[Tooltip("目前已使用次數（執行期會成長）")]
	[SerializeField] private int currentScrubCount = 0;

	private bool isBathing;
	private BubblePool bubblePool;
	private float bubbleAccumulator;
	private float accumulatedScrubDistance;
	private Vector3 lastDragPos;
	private bool hasLastDragPos;

	protected override void Awake()
	{
		base.Awake();
		var col = GetComponent<Collider2D>();
		if (col != null) col.isTrigger = true;
		if (pet == null) pet = FindObjectOfType<PetInteractionState>();
		// 預設將泡泡父節點指向寵物
		if (bubbleParent == null && pet != null) bubbleParent = pet.transform;
		EnsureBubblePool();
	}

	private void EnsureBubblePool()
	{
		if (pet == null) return;
		bubblePool = pet.GetComponent<BubblePool>();
		if (bubblePool == null)
		{
			bubblePool = pet.gameObject.AddComponent<BubblePool>();
		}
		// 將這次 Soap 的預置與大小配置進池（僅在未設定時或擴增大小時生效）
		var parent = bubbleParent != null ? bubbleParent : pet.transform;
		bubblePool.ConfigureIfEmpty(bubblePrefab, bubblePoolSize, parent);
		bubblePool.EnsureInitialized();
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (pet == null) return;
		if (!other.transform.IsChildOf(pet.transform) && other.transform != pet.transform) return;
		StartBath();
		lastDragPos = transform.position;
		hasLastDragPos = true;
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

		// 計算拖拽移動距離以推進使用次數
		if (hasLastDragPos)
		{
			float delta = (transform.position - lastDragPos).magnitude;
			if (delta > 0f)
			{
				accumulatedScrubDistance += delta;
				while (accumulatedScrubDistance >= scrubDistancePerCount)
				{
					accumulatedScrubDistance -= scrubDistancePerCount;
					currentScrubCount++;
					if (currentScrubCount >= maxScrubCount)
					{
						StopBath();
						Destroy(gameObject);
						return;
					}
				}
			}
			lastDragPos = transform.position;
		}
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
		accumulatedScrubDistance = 0f;
		hasLastDragPos = false;
		if (pet != null)
		{
			// 進入互動（Bath），若有設定搓澡音效則播放一次
			pet.BeginInteraction(bathStateName, bathSfx, bathSfxVolume);
		}
	}

	private void StopBath()
	{
		if (!isBathing) return;
		isBathing = false;
		hasLastDragPos = false;
		if (pet != null)
		{
			pet.EndInteraction();
		}
	}

	private void SpawnBubbles(float deltaTime)
	{
		if (bubblePool == null) return;
		if (bubblesPerSecond <= 0f) return;
		bubbleAccumulator += bubblesPerSecond * deltaTime;
		int count = Mathf.FloorToInt(bubbleAccumulator);
		if (count <= 0) return;
		bubbleAccumulator -= count;

		for (int i = 0; i < count; i++)
		{
			var go = bubblePool.Get();
			if (go == null) return;
			Vector2 rnd = Random.insideUnitCircle * bubbleRadius;
			Vector3 center = pet != null ? pet.transform.position : transform.position;
			go.transform.position = new Vector3(center.x + rnd.x, center.y + rnd.y, center.z);
			go.SetActive(true);
			var eff = go.GetComponent<BubbleEffect>();
			if (eff != null) eff.Play();
		}
	}
}


