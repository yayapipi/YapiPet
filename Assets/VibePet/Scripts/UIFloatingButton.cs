using UnityEngine;

namespace YFrame.Runtime.UIFrame.Y_UIAnim
{
	// 讓 UI Button（或任意 UI 元件）以正弦曲線在空中緩緩漂浮
	[RequireComponent(typeof(RectTransform))]
	public class UIFloatingButton : MonoBehaviour
	{
		[Header("漂浮參數")]
		public float amplitudeY = 10f;      // 垂直漂浮幅度（像素）
		public float amplitudeX = 0f;       // 水平漂浮幅度（像素）
		public float frequency = 0.5f;      // 漂浮頻率（次/秒）
		public float phaseOffset = 0f;      // 相位偏移（弧度）
		public bool randomizePhaseOnEnable = true; // 啟用時是否隨機相位

		[Header("擺動旋轉")]
		public float swayRotation = 0f;     // 輕微擺動角度（度），0 代表不旋轉
		public float rotationFrequency = 0.5f; // 旋轉頻率（次/秒）

		[Header("時間來源")]
		public bool useUnscaledTime = true; // 使用 unscaled time（暫停時也持續播放）

		private RectTransform rectTransform;
		private Vector2 baseAnchoredPos;
		private Quaternion baseLocalRotation;

		private void Awake()
		{
			rectTransform = GetComponent<RectTransform>();
			baseLocalRotation = rectTransform.localRotation;
		}

		private void OnEnable()
		{
			baseAnchoredPos = rectTransform.anchoredPosition;
			if (randomizePhaseOnEnable)
			{
				phaseOffset = Random.Range(0f, Mathf.PI * 2f);
			}
		}

		private void Update()
		{
			float t = useUnscaledTime ? Time.unscaledTime : Time.time;
			float omega = Mathf.PI * 2f * Mathf.Max(0f, frequency);
			float angle = omega * t + phaseOffset;

			float dx = amplitudeX * Mathf.Sin(angle);
			float dy = amplitudeY * Mathf.Sin(angle);
			rectTransform.anchoredPosition = baseAnchoredPos + new Vector2(dx, dy);

			if (swayRotation != 0f)
			{
				float rotOmega = Mathf.PI * 2f * Mathf.Max(0f, rotationFrequency);
				float rotAngle = rotOmega * t + phaseOffset;
				float z = swayRotation * Mathf.Sin(rotAngle);
				rectTransform.localRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, z);
			}
		}
	}
}


