using UnityEngine;

/// <summary>
/// 定義寵物可移動的矩形範圍，並在 Editor 中以 Gizmos 繪製。
/// - 可指定世界座標中心與尺寸；
/// - 提供 Clamp 與隨機點取得；
/// - 建議與 PetAIController 搭配，限制北極熊隨機走動範圍。
/// </summary>
public class PetMoveArea : MonoBehaviour
{
	[Header("Area (World Space)")]
	[Tooltip("可移動區域中心（世界座標）")]
	[SerializeField] private Vector2 areaCenter = Vector2.zero;
	[Tooltip("可移動區域尺寸（世界單位）")]
	[SerializeField] private Vector2 areaSize = new Vector2(8f, 4f);
	[Tooltip("在 Scene 檢視中繪製的顏色（選中時）")]
	[SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.35f);

	/// <summary>
	/// 取得區域矩形（世界座標）
	/// </summary>
	public Rect GetWorldRect()
	{
		Vector2 size = areaSize;
		Vector2 min = areaCenter - size * 0.5f;
		return new Rect(min, size);
	}

	/// <summary>
	/// 將世界座標點夾到可移動範圍內
	/// </summary>
	public Vector3 ClampToArea(Vector3 worldPosition)
	{
		Rect r = GetWorldRect();
		float x = Mathf.Clamp(worldPosition.x, r.xMin, r.xMax);
		float y = Mathf.Clamp(worldPosition.y, r.yMin, r.yMax);
		return new Vector3(x, y, worldPosition.z);
	}

	/// <summary>
	/// 取得區域內的隨機世界座標點
	/// </summary>
	public Vector3 GetRandomWorldPoint(float fixedZ)
	{
		Rect r = GetWorldRect();
		float x = Random.Range(r.xMin, r.xMax);
		float y = Random.Range(r.yMin, r.yMax);
		return new Vector3(x, y, fixedZ);
	}

	/// <summary>
	/// 在 Scene 視窗中繪製可視化矩形範圍
	/// </summary>
	private void OnDrawGizmosSelected()
	{
		Color old = Gizmos.color;
		Gizmos.color = gizmoColor;
		Rect r = GetWorldRect();
		Vector3 center = new Vector3(r.center.x, r.center.y, 0f);
		Vector3 size3 = new Vector3(r.size.x, r.size.y, 0f);
		Gizmos.DrawWireCube(center, size3);
		Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoColor.a * 0.15f);
		Gizmos.DrawCube(center, size3);
		Gizmos.color = old;
	}
}


