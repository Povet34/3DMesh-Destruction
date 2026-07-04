using UnityEngine;
using Povet.MeshDestruction;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 클릭한 오브젝트의 FracturedSwap을 폭발시키는 데모 컴포넌트.
// 구 Input Manager / 신 Input System 양쪽에서 동작함.
public class ExplodeOnClick : MonoBehaviour
{
    public Camera targetCamera;
    public float rayDistance = 100f;

    void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
    }

    void Update()
    {
        bool clicked;
        Vector3 mousePosition;

#if ENABLE_INPUT_SYSTEM
        clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        mousePosition = Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
#else
        clicked = Input.GetMouseButtonDown(0);
        mousePosition = Input.mousePosition;
#endif

        if (!clicked || targetCamera == null) return;

        Ray ray = targetCamera.ScreenPointToRay(mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
        {
            FracturedSwap swap = hit.collider.GetComponentInParent<FracturedSwap>();
            if (swap != null)
                swap.Explode(hit.point);
        }
    }
}
