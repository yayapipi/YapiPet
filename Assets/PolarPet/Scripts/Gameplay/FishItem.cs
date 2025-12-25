using UnityEngine;

/// <summary>
/// 魚道具：
/// - 可拖拽；
/// - 放到寵物身上被餵食，播放寵物吃動畫與音效；
/// - 吃完後道具消失。
/// 使用方式：掛在 Fish 預置體上，確保有 2D Collider 並開啟 isTrigger。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FishItem : DraggableItem
{
	[Header("Pet Interaction")]
	[Tooltip("寵物 GameObject（未指定則在場景中尋找帶有 PetInteractionState 的物件）")]
	[SerializeField] private PetInteractionState pet;
	[Tooltip("餵食時寵物播放的動畫狀態名")]
	[SerializeField] private string eatStateName = "Eat";
	[Tooltip("餵食音效（播放在寵物的 AudioSource 上）")]
	[SerializeField] private AudioClip eatSfx;
	[Tooltip("音效音量")]
	[SerializeField] [Range(0f, 1f)] private float sfxVolume = 1f;
	[Tooltip("當魚接觸寵物後自動觸發餵食，避免需要額外點擊")]
	[SerializeField] private bool autoFeedOnContact = true;
	[Tooltip("餵食後自動恢復寵物狀態的延遲秒數（覆蓋寵物預設）")]
	[SerializeField] private float autoResumeAfterEat = 1.0f;

	private bool consumed;

	protected override void Awake()
	{
		base.Awake();
		var col = GetComponent<Collider2D>();
		if (col != null)
		{
			col.isTrigger = true;
		}
		if (pet == null)
		{
			pet = FindObjectOfType<PetInteractionState>();
		}
	}

	/// <summary>
	/// 當魚與寵物接觸時觸發餵食
	/// </summary>
	private void OnTriggerEnter2D(Collider2D other)
	{
		if (consumed) return;
		if (pet == null) return;
		if (!autoFeedOnContact) return;
		if (!other.transform.IsChildOf(pet.transform) && other.transform != pet.transform) return;

		FeedPet();
	}

	/// <summary>
	/// 執行餵食邏輯：鎖寵物、播放動畫與音效，然後銷毀自己
	/// </summary>
	private void FeedPet()
	{
		if (consumed) return;
		consumed = true;
		if (pet != null)
		{
			float prev = 0f;
			pet.SetAutoResumeSeconds(autoResumeAfterEat);
			pet.BeginInteraction(eatStateName, eatSfx, sfxVolume);
		}
		// 可加一點延遲表現吃的時間。若無特別需求，立即銷毀道具
		Destroy(gameObject);
	}
}


