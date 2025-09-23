using UnityEngine;

/// <summary>
/// 掛在寵物身上的互動狀態管理：
/// - 外部可請求進入/離開「被互動」狀態（例如吃、洗澡）；
/// - 設定與播放指定動畫、音效；
/// - 被互動期間可鎖定移動（例如暫停拖拽或 AI 移動）。
/// </summary>
[RequireComponent(typeof(Animator))]
public class PetInteractionState : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private Animator animator;
	[SerializeField] private AudioSource audioSource;

	[Header("Movement Lock")]
	[Tooltip("互動中是否鎖定寵物移動/拖拽")]
	[SerializeField] private bool lockMovementDuringInteraction = true;

	private bool isInteracting;
	private string currentStateName;
	private string resumeStateName = "Walk_bear";

	[Header("Auto Resume")]
	[Tooltip("互動後自動恢復的延遲秒數；<=0 表示不自動恢復")]
	[SerializeField] private float autoResumeSeconds = 0f;

	/// <summary>
	/// 快取常用元件
	/// </summary>
	private void Awake()
	{
		if (animator == null)
		{
			animator = GetComponentInChildren<Animator>();
		}
		if (audioSource == null)
		{
			audioSource = GetComponent<AudioSource>();
			if (audioSource == null)
			{
				audioSource = gameObject.AddComponent<AudioSource>();
			}
		}
	}

	/// <summary>
	/// 進入互動狀態，切換動畫與音效，必要時鎖移動
	/// </summary>
	public void BeginInteraction(string stateName, AudioClip sfx, float volume = 1f)
	{
		if (isInteracting) return;
		isInteracting = true;
		currentStateName = stateName;
		PlayAnimation(stateName);

		if (sfx != null)
		{
			PlaySfx(sfx, volume);
		}

		if (lockMovementDuringInteraction)
		{
			// 若有拖拽控制，讓其暫停
			var drag = GetComponent<PetDragController>();
			if (drag != null)
			{
				// 使用 Animator 狀態也可表現「不移動」，這裡只確保不被拖拽
				// 讓外界無法通過 OnMouseDown 啟動拖拽
				drag.enabled = false;
			}
		}

		if (autoResumeSeconds > 0f)
		{
			CancelInvoke(nameof(EndInteraction));
			Invoke(nameof(EndInteraction), autoResumeSeconds);
		}
	}

	/// <summary>
	/// 結束互動狀態，恢復預設動畫與移動
	/// </summary>
	public void EndInteraction()
	{
		if (!isInteracting) return;
		isInteracting = false;
		currentStateName = null;
		PlayAnimation(resumeStateName);

		if (lockMovementDuringInteraction)
		{
			var drag = GetComponent<PetDragController>();
			if (drag != null)
			{
				drag.enabled = true;
			}
		}
	}

	/// <summary>
	/// 設置互動結束後恢復的動畫狀態名稱（預設 Walk_bear）
	/// </summary>
	public void SetResumeState(string stateName)
	{
		resumeStateName = stateName;
	}

	/// <summary>
	/// 設定自動恢復秒數（<=0 表示不自動）
	/// </summary>
	public void SetAutoResumeSeconds(float seconds)
	{
		autoResumeSeconds = seconds;
	}

	private void PlayAnimation(string stateName)
	{
		if (animator == null || string.IsNullOrEmpty(stateName)) return;
		animator.Play(stateName);
	}

	private void PlaySfx(AudioClip clip, float volume)
	{
		if (audioSource == null || clip == null) return;
		audioSource.volume = volume;
		audioSource.PlayOneShot(clip);
	}
}


