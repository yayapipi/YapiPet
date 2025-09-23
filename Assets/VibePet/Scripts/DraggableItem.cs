using UnityEngine;

/// <summary>
/// 可掛載於道具 Prefab 的通用拖拽行為：
/// - 滑鼠按下開始拖拽，跟隨滑鼠移動；
/// - 滑鼠放開結束拖拽；
/// - 若存在 Rigidbody2D（Kinematic），使用 MovePosition 以穩定觸發 2D 事件；
/// 設計：僅在拖拽期間更新位置，減少每幀負擔。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DraggableItem : MonoBehaviour
{
	[Header("Drag Settings")]
	[SerializeField] private float dragWorldZ = 0f;
	[SerializeField] private Camera targetCamera;
	[SerializeField] private float maxDragSpeed = 0f;

	protected bool isDragging;
	private Vector3 dragOffsetWorld;
	protected Rigidbody2D rb2d;

	/// <summary>
	/// 快取相機/剛體
	/// </summary>
	protected virtual void Awake()
	{
		if (targetCamera == null)
		{
			targetCamera = Camera.main;
		}
		rb2d = GetComponent<Rigidbody2D>();
	}

	/// <summary>
	/// 滑鼠按下開始拖拽
	/// </summary>
	protected virtual void OnMouseDown()
	{
		if (targetCamera == null) return;
		isDragging = true;
		Vector3 mouseWorld = GetMouseWorldPosition();
		dragOffsetWorld = transform.position - mouseWorld;
		OnDragStart();
	}

	/// <summary>
	/// 滑鼠放開結束拖拽
	/// </summary>
	protected virtual void OnMouseUp()
	{
		if (!isDragging) return;
		isDragging = false;
		OnDragEnd();
	}

	/// <summary>
	/// 拖拽中更新位置
	/// </summary>
	protected virtual void Update()
	{
		if (!isDragging) { OnUpdateAfterDrag(); return; }
		if (targetCamera == null) return;

		if (Input.GetMouseButtonUp(0))
		{
			isDragging = false;
			OnDragEnd();
			return;
		}

		Vector3 mouseWorld = GetMouseWorldPosition();
		Vector3 targetPos = mouseWorld + dragOffsetWorld;
		targetPos.z = dragWorldZ;

		if (rb2d != null && rb2d.bodyType == RigidbodyType2D.Kinematic)
		{
			Vector2 nextPos2D;
			if (maxDragSpeed > 0f)
			{
				float maxDelta = maxDragSpeed * Time.deltaTime;
				Vector3 next = Vector3.MoveTowards(transform.position, targetPos, maxDelta);
				nextPos2D = new Vector2(next.x, next.y);
			}
			else
			{
				nextPos2D = new Vector2(targetPos.x, targetPos.y);
			}
			rb2d.MovePosition(nextPos2D);
			var p = transform.position; p.z = targetPos.z; transform.position = p;
		}
		else
		{
			if (maxDragSpeed > 0f)
			{
				float maxDelta = maxDragSpeed * Time.deltaTime;
				transform.position = Vector3.MoveTowards(transform.position, targetPos, maxDelta);
			}
			else
			{
				transform.position = targetPos;
			}
		}

		OnDragging();
	}

	protected virtual void OnDragStart() { }
	protected virtual void OnDragging() { }
	protected virtual void OnDragEnd() { }
	protected virtual void OnUpdateAfterDrag() { }

	/// <summary>
	/// 從外部（例如 UI 拖拽）啟動拖拽：
	/// - 可選覆寫相機與拖拽目標 Z；
	/// - 可選是否先將物件中心吸附到當前指標位置；
	/// - 自動計算拖拽偏移，之後由本組件在 Update 中跟隨。
	/// </summary>
	public void BeginDragFromUI(Camera cameraOverride = null, float? overrideDragWorldZ = null, bool snapToPointer = true)
	{
		if (cameraOverride != null)
		{
			targetCamera = cameraOverride;
		}
		if (overrideDragWorldZ.HasValue)
		{
			dragWorldZ = overrideDragWorldZ.Value;
		}

		// 先將物件移動到指標位置（可選），確保 Z 正確
		if (snapToPointer && targetCamera != null)
		{
			Vector3 screenPos = Input.mousePosition;
			screenPos.z = Mathf.Abs(targetCamera.transform.position.z - dragWorldZ);
			Vector3 world = targetCamera.ScreenToWorldPoint(screenPos);
			world.z = dragWorldZ;
			transform.position = world;
		}

		// 啟動拖拽並計算偏移
		isDragging = true;
		if (targetCamera != null)
		{
			Vector3 mouseWorld = GetMouseWorldPosition();
			dragOffsetWorld = transform.position - mouseWorld;
		}
		else
		{
			dragOffsetWorld = Vector3.zero;
		}
		OnDragStart();
	}

	private Vector3 GetMouseWorldPosition()
	{
		Vector3 screenPos = Input.mousePosition;
		screenPos.z = Mathf.Abs(targetCamera.transform.position.z - transform.position.z);
		Vector3 world = targetCamera.ScreenToWorldPoint(screenPos);
		world.z = dragWorldZ;
		return world;
	}
}


