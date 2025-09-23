using UnityEngine;

/// <summary>
/// 控制寵物在場景中被滑鼠拖拽的行為：
/// 1. 按下滑鼠並拖拽時切換動畫為 Drag。
/// 2. 放開滑鼠時恢復動畫為 Walk_bear。
/// 3. 拖拽移動時依照左右方向 Flip X。
/// 4. 拖拽時顯示陰影，放開時隱藏陰影。
/// 
/// 設計原則：
/// - 盡量使用序列化欄位以便從 Inspector 設定。
/// - 在 Awake 快取常用組件，避免每幀查找。
/// - 不依賴 Update 輪詢，僅在拖拽狀態時更新。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PetDragController : MonoBehaviour
{
	[Header("References")]
	[Tooltip("Animator，用於切換 Drag / Walk_bear 動畫")] 
	[SerializeField] private Animator animator;
	[Tooltip("主要 Sprite 用於 FlipX，若為 SpriteRenderer 放這裡；若是整個物件旋轉，改為 Transform 對象。")]
	[SerializeField] private SpriteRenderer spriteRenderer;
	[Tooltip("陰影 GameObject（例如一個半透明橢圓），拖拽時顯示")]
	[SerializeField] private GameObject shadowObject;

	[Header("Animation Names")]
	[Tooltip("拖拽時播放的動畫狀態名稱")] 
	[SerializeField] private string dragStateName = "Drag";
	[Tooltip("放手後恢復的動畫狀態名稱")] 
	[SerializeField] private string idleStateName = "Walk_bear";

	[Header("Drag Settings")]
	[Tooltip("拖拽時的 Z 軸世界座標（確保在合適排序層級）")]
	[SerializeField] private float dragWorldZ = 0f;
	[Tooltip("將螢幕滑鼠位置轉世界座標時的相機，若留空則在 Awake 取得主相機")]
	[SerializeField] private Camera targetCamera;
	[Tooltip("是否限制拖拽移動速度（避免瞬間跳動）。0 或負值表示不限制")]
	[SerializeField] private float maxDragSpeed = 0f;
	[Tooltip("Flip 方向判定的最小位移閾值，避免 微小抖動 觸發 Flip")]
	[SerializeField] private float flipMinDelta = 0.001f;

	private bool isDragging;
	private Vector3 dragOffsetWorld;
	private Vector3 lastWorldPos;

	/// <summary>
	/// 快取相機與必要組件
	/// </summary>
	private void Awake()
	{
		if (targetCamera == null)
		{
			targetCamera = Camera.main;
		}

		if (animator == null)
		{
			animator = GetComponentInChildren<Animator>();
		}

		if (spriteRenderer == null)
		{
			spriteRenderer = GetComponentInChildren<SpriteRenderer>();
		}

		if (shadowObject != null)
		{
			shadowObject.SetActive(false);
		}
		else if (spriteRenderer != null)
		{
			var shadowGO = new GameObject("Shadow");
			shadowGO.transform.SetParent(transform, false);
			shadowGO.transform.localPosition = new Vector3(0f, -0.6f, 0f);
			shadowGO.transform.localScale = new Vector3(1.0f, 0.35f, 1f);

			var shadowSr = shadowGO.AddComponent<SpriteRenderer>();
			shadowSr.sprite = spriteRenderer.sprite;
			shadowSr.color = new Color(0f, 0f, 0f, 0.25f);
			shadowSr.sortingLayerID = spriteRenderer.sortingLayerID;
			shadowSr.sortingOrder = spriteRenderer.sortingOrder - 1;

			shadowObject = shadowGO;
			shadowObject.SetActive(false);
		}
	}

	/// <summary>
	/// 滑鼠按下時進入拖拽狀態
	/// </summary>
	private void OnMouseDown()
	{
		if (targetCamera == null) return;

		isDragging = true;
		Vector3 mouseWorld = GetMouseWorldPosition();
		dragOffsetWorld = transform.position - mouseWorld;
		lastWorldPos = transform.position;

		PlayAnimation(dragStateName);
		SetShadowActive(true);
	}

	/// <summary>
	/// 滑鼠釋放時離開拖拽狀態
	/// </summary>
	private void OnMouseUp()
	{
		isDragging = false;
		PlayAnimation(idleStateName);
		SetShadowActive(false);
	}

	/// <summary>
	/// 拖拽中更新位置與 Flip
	/// </summary>
private void Update()
{
	if (!isDragging) return;
	if (targetCamera == null) return;

	// 若拖拽中，但滑鼠在物件外放開，也要正確結束拖拽
	if (Input.GetMouseButtonUp(0))
	{
		isDragging = false;
		PlayAnimation(idleStateName);
		SetShadowActive(false);
		return;
	}

	Vector3 mouseWorld = GetMouseWorldPosition();
	Vector3 targetPos = mouseWorld + dragOffsetWorld;
	targetPos.z = dragWorldZ;

	if (maxDragSpeed > 0f)
	{
		float maxDelta = maxDragSpeed * Time.deltaTime;
		transform.position = Vector3.MoveTowards(transform.position, targetPos, maxDelta);
	}
	else
	{
		transform.position = targetPos;
	}

	Vector3 delta = transform.position - lastWorldPos;
	if (Mathf.Abs(delta.x) > flipMinDelta)
	{
		SetFlipX(delta.x < 0f);
	}
	lastWorldPos = transform.position;
}

	/// <summary>
	/// 依名稱切換 Animator 狀態
	/// </summary>
	private void PlayAnimation(string stateName)
	{
		if (animator == null || string.IsNullOrEmpty(stateName)) return;
		animator.Play(stateName);
	}

	/// <summary>
	/// 控制陰影顯示
	/// </summary>
	private void SetShadowActive(bool active)
	{
		if (shadowObject == null) return;
		if (shadowObject.activeSelf == active) return;
		shadowObject.SetActive(active);
	}

	/// <summary>
	/// 設定 SpriteRenderer 的 FlipX
	/// </summary>
	private void SetFlipX(bool flip)
	{
		if (spriteRenderer == null) return;
		spriteRenderer.flipX = flip;
	}

	/// <summary>
	/// 從滑鼠螢幕座標換算世界座標
	/// </summary>
	private Vector3 GetMouseWorldPosition()
	{
		Vector3 screenPos = Input.mousePosition;
		screenPos.z = Mathf.Abs(targetCamera.transform.position.z - transform.position.z);
		Vector3 world = targetCamera.ScreenToWorldPoint(screenPos);
		world.z = dragWorldZ;
		return world;
	}


private void OnDisable()
{
	if (isDragging)
	{
		isDragging = false;
		SetShadowActive(false);
		PlayAnimation(idleStateName);
	}
}
}
