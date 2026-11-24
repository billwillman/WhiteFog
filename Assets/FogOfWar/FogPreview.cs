using UnityEngine;

// Independent preview controls. All fog values and curves live on FogOfWar and FogPreset.
public sealed class FogPreview : MonoBehaviour
{
    public Camera view;
    public FogOfWar fog;
    public Transform focus;
    Vector2 point;
    float distanceFromFocus = 25f;
    readonly AnimationCurve fieldOfView = new AnimationCurve(new Keyframe(0, 0, 2, 2), new Keyframe(1, 1, .28165814f, .28165814f));
    readonly AnimationCurve pitch = new AnimationCurve(new Keyframe(0, 0, 2, 2), new Keyframe(1, 1, 0, 0));

    void FrameView()
    {
        float zoom = Mathf.InverseLerp(10, 90, distanceFromFocus);
        view.transform.rotation = Quaternion.Euler(Mathf.Lerp(40, 60, pitch.Evaluate(zoom)), view.transform.eulerAngles.y, 0);
        view.fieldOfView = Mathf.Lerp(42, 32, fieldOfView.Evaluate(zoom));
        view.transform.position = focus.position - view.transform.forward * distanceFromFocus;
    }

    void Start()
    {
        point = new Vector2(focus.position.x, focus.position.z);
        FrameView();
        fog.SetVisibility(point, 2, 4);
    }
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = view.ScreenPointToRay(Input.mousePosition);
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float hit))
            {
                Vector3 p = ray.GetPoint(hit);
                point = new Vector2(p.x, p.z);
                fog.SetVisibility(point, Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 1 : 2, 4);
            }
        }
        if (Input.GetMouseButtonDown(1)) fog.HideAll();
        if (Input.GetKeyDown(KeyCode.Alpha1)) fog.SetVisibility(point, 0, 4);
        if (Input.GetKeyDown(KeyCode.Alpha2)) fog.SetVisibility(point, 1, 4);
        if (Input.GetKeyDown(KeyCode.Alpha3)) fog.SetVisibility(point, 2, 4);
        if (Input.GetKeyDown(KeyCode.Space)) fog.animateTime = !fog.animateTime;
        float scrub = (Input.GetKey(KeyCode.RightBracket) ? 1 : 0) - (Input.GetKey(KeyCode.LeftBracket) ? 1 : 0);
        fog.timeOfDay = Mathf.Repeat(fog.timeOfDay + scrub * Time.deltaTime * 0.15f, 1);
        float wheel = Input.mouseScrollDelta.y;
        if (wheel != 0)
        {
            distanceFromFocus = Mathf.Clamp(distanceFromFocus - wheel * 4.6f, 10, 90);
            FrameView();
        }
    }
}
