using UnityEngine;
using UnityEngine.EventSystems;

namespace SoftKitty.MasterCombatCore.Demo
{
    public class CameraControl : MonoBehaviour
    {
       
        public Transform RotY;
        public Transform RotX;
        public Camera Cam;
        public Vector2 ZoomLimit;
        public bool HoldRightMouseForRotate = true;
        [Range(0.5F,4F)]
        public float RotateSpeed = 1F;
        [HideInInspector]
        public float fovAdd = 0F;
        [HideInInspector]
        public float Shake = 0F;
        float angleY = 0F;
        float angleX = 20F;
        float offsetY = 1.5F;
        float zoom = 0.5F;

        float _angleY = 0F;
        float _angleX = 20F;
        float _offsetY = 1.5F;
        float _zoom = 0.5F;
        float _zoomBlock = 1F;
        float fov;
        RaycastHit _hit;
        Transform Player;

        void Start()
        {
            fov = Cam.fieldOfView;
        }

        public void AssignPlayer(Transform _player)
        {
            Player = _player;
            transform.position = Player.position;
        }


        void Update()
        {
            if (Player == null) return;
            Shake = Mathf.Lerp(Shake,0F,Time.deltaTime*2.5F);
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                if (InputProxy.GetMouseButton(1) || !HoldRightMouseForRotate)
                {
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    angleY = angleY + InputProxy.GetMouseX() * Time.deltaTime * 150F;
                    angleX = Mathf.Clamp(angleX - InputProxy.GetMouseY() * Time.deltaTime * 50F, 0F, 89F);
                }
                else
                {
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                }
                zoom = Mathf.Clamp(zoom - InputProxy.GetMouseScrollWheel() * Time.deltaTime * 100F, 0.3F, 1F);
            }

            _zoom = Mathf.Lerp(_zoom, zoom,Time.deltaTime*2F);
            _offsetY= Mathf.Lerp(_offsetY, offsetY, Time.deltaTime * 5F);
            _angleY = Mathf.Lerp(_angleY, angleY, Time.deltaTime * 10F* RotateSpeed);
            _angleX = Mathf.Lerp(_angleX, angleX, Time.deltaTime * 10F * RotateSpeed);
            if (Physics.Linecast(RotY.position- Cam.transform.forward, RotY.position - Cam.transform.forward* Mathf.Lerp(ZoomLimit.x, ZoomLimit.y, zoom), out _hit, SGD_Settings.Instance.GroundLayer, QueryTriggerInteraction.Ignore))
            {
                _zoomBlock = (_hit.distance-0.5F) / (ZoomLimit.y-0.5F);
            }
            else
            {
                _zoomBlock = _zoom;
            }
            if (_zoom > _zoomBlock) _zoom = _zoomBlock;
        }

        private void LateUpdate()
        {
            if (Player == null) return;
            transform.position = Player.transform.position;
            RotY.localPosition = new Vector3(0F,_offsetY,0F);
            RotY.localEulerAngles = new Vector3(0F, _angleY,0F);
            RotX.localEulerAngles = new Vector3(_angleX, 0F, 0F);
            Cam.transform.localPosition = new Vector3(0F,0F,-Mathf.Lerp(ZoomLimit.x, ZoomLimit.y, _zoom));
            Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, fov + fovAdd, Time.deltaTime*5F);
            fovAdd = Mathf.Lerp(fovAdd,0F,Time.deltaTime);
            Cam.transform.localEulerAngles = new Vector3(Mathf.Sin(Time.time*Shake*1F)*Shake*2F, Mathf.Sin(Time.time * Shake * 1.2F) * Shake * 2.5F, Mathf.Sin(Time.time * Shake * 0.8F) * Shake * 1.5F);
        }
       

    }
}
