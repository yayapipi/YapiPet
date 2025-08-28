// 8/28/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class RandomMoveAndDrag : MonoBehaviour
{
    public Vector2 moveRange = new Vector2(5f, 5f); // The range of random movement
    public float moveSpeed = 2f; // Movement speed

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private bool isDragging = false;
    private Vector3 targetPosition;
    [Header("Debug")]
    public bool enableDebugLog = true;
    private void D(string msg)
    {
        if (enableDebugLog) Debug.Log($"[RandomMoveAndDrag] {msg}");
    }
    // 用于拖拽时判断左右移动的上一帧鼠标世界坐标X
    private float? lastDragWorldX = null;

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        D($"Start. Initial position={transform.position}");
        SetRandomTargetPosition();
    }

    private void Update()
    {
        if (isDragging)
            return;

        MoveTowardsTarget();
        CheckIfTargetReached();
    }

    private void OnMouseDown()
    {
        isDragging = true;
        animator.Play($"Drag"); // Switch to Drag animation
        D($"OnMouseDown -> begin dragging at {transform.position}");
        // 记录初始鼠标世界坐标X，作为拖拽对比基准
        var world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        lastDragWorldX = world.x;
    }

    private void OnMouseDrag()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = transform.position.z; // Keep the Z position unchanged

        // 根据鼠标X位移方向翻转
        if (lastDragWorldX.HasValue)
        {
            float dx = mousePosition.x - lastDragWorldX.Value;
            const float threshold = 0.0001f; // 小阈值避免抖动
            if (dx > threshold)
                spriteRenderer.flipX = false; // 右移
            else if (dx < -threshold)
                spriteRenderer.flipX = true;  // 左移
        }
        lastDragWorldX = mousePosition.x;

        transform.position = mousePosition;
        D($"OnMouseDrag -> position={transform.position}");
    }

    private void OnMouseUp()
    {
        isDragging = false;
        animator.Play("Walk_bear"); // Switch back to Walking animation
        // 结束拖拽，清空记录
        lastDragWorldX = null;
    }

    private void MoveTowardsTarget()
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;

        // Flip sprite based on movement direction
        if (direction.x > 0)
            spriteRenderer.flipX = false;
        else if (direction.x < 0)
            spriteRenderer.flipX = true;
    }

    private void CheckIfTargetReached()
    {
        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            SetRandomTargetPosition();
        }
    }

    private void SetRandomTargetPosition()
    {
        Vector3 randomOffset = new Vector3(
            Random.Range(-moveRange.x, moveRange.x),
            Random.Range(-moveRange.y, moveRange.y),
            0f
        );
        targetPosition = transform.position + randomOffset;
    }
}