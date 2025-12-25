using UnityEngine;
using System.Collections;

/// <summary>
/// 泡泡的簡易特效：上升、放大、漸隱，結束後自動關閉物件以利物件池重用。
/// </summary>
public class BubbleEffect : MonoBehaviour
{
	[SerializeField] private float lifetime = 1.2f;
	[SerializeField] private float riseSpeed = 0.7f;
	[SerializeField] private float startScale = 0.6f;
	[SerializeField] private float endScale = 1.0f;

	private float timer;
	private SpriteRenderer sr;
	private Color originalColor;

	private void Awake()
	{
		sr = GetComponentInChildren<SpriteRenderer>();
		if (sr != null) originalColor = sr.color;
	}

	public void Play()
	{
		timer = 0f;
		if (sr != null) sr.color = originalColor;
		transform.localScale = Vector3.one * startScale;
		StopAllCoroutines();
		StartCoroutine(CoPlay());
	}

	private IEnumerator CoPlay()
	{
		while (timer < lifetime)
		{
			timer += Time.deltaTime;
			float t = Mathf.Clamp01(timer / lifetime);
			transform.position += Vector3.up * riseSpeed * Time.deltaTime;
			transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
			if (sr != null)
			{
				Color c = sr.color; c.a = 1f - t; sr.color = c;
			}
			yield return null;
		}
		gameObject.SetActive(false);
	}
}


