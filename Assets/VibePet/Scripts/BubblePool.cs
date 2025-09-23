using UnityEngine;

/// <summary>
/// 掛在寵物上的泡泡物件池：
/// - 預先生成固定數量泡泡並重複使用；
/// - 供 SoapItem 取得泡泡實例；
/// - 透過 ConfigureIfEmpty/EnsureInitialized 控制首次配置與預熱。
/// </summary>
public class BubblePool : MonoBehaviour
{
	[SerializeField] private GameObject bubblePrefab;
	[SerializeField] private int poolSize = 24;
	[SerializeField] private Transform parentTransform;

	private GameObject[] pool;
	private int nextIndex;

	private void Awake()
	{
		if (parentTransform == null) parentTransform = transform;
	}

	/// <summary>
	/// 若尚未設定或尚未初始化，套用外部提供的預置與大小（不會覆蓋已存在設定）。
/// </summary>
	public void ConfigureIfEmpty(GameObject prefab, int size, Transform parent)
	{
		if (bubblePrefab == null && prefab != null)
		{
			bubblePrefab = prefab;
		}
		if (size > poolSize)
		{
			poolSize = size;
		}
		if (parentTransform == null && parent != null)
		{
			parentTransform = parent;
		}
	}

	/// <summary>
	/// 若物件池尚未建立，進行預熱建立。
/// </summary>
	public void EnsureInitialized()
	{
		if (pool != null) return;
		if (parentTransform == null) parentTransform = transform;
		if (bubblePrefab == null || poolSize <= 0)
		{
			pool = new GameObject[0];
			return;
		}
		pool = new GameObject[poolSize];
		for (int i = 0; i < poolSize; i++)
		{
			pool[i] = Instantiate(bubblePrefab, parentTransform);
			pool[i].SetActive(false);
		}
		nextIndex = 0;
	}

	/// <summary>
	/// 取得一個泡泡實例：若池滿則覆用最舊的一個（先設為關閉再交付）。
/// </summary>
	public GameObject Get()
	{
		if (pool == null || pool.Length == 0) return null;
		// 掃描找一個閒置的
		for (int n = 0; n < pool.Length; n++)
		{
			int idx = (nextIndex + n) % pool.Length;
			if (!pool[idx].activeSelf)
			{
				nextIndex = (idx + 1) % pool.Length;
				return pool[idx];
			}
		}
		// 全忙碌則覆用 nextIndex
		var go = pool[nextIndex];
		go.SetActive(false);
		nextIndex = (nextIndex + 1) % pool.Length;
		return go;
	}
}


