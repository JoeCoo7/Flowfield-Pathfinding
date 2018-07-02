using ECSInput;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using Math = System.Math;

//----------------------------------------------------------------------------------------
namespace WT.ECS
{
   
    //----------------------------------------------------------------------------------------
    public class RTSCameraSettings : MonoBehaviour
    {
        public GameObject Target;
        public Vector3 StartPosition = new Vector3(0, 0, 0);

        public const int PAN_LEFT = 1;
        public const int PAN_RIGHT = 2;
        public const int PAN_DOWN = 4;
        public const int PAN_UP = 8;

        public const int NO_ZOOM = 0;
        public const int ZOOM_KEY = 1;
        public const int ZOOM_WHEEL = 2;

        [Header("Features")]
        public bool AllowEdgePanning;
        public bool AllowKeyPanning;
        public bool AllowMouseDragPanning;
        public bool AllowWheelZooming;
        public bool AllowKeyZooming;
        public bool AllowZoomTilting;
        public bool AllowMouseRotation;
        public bool AllowMoveTo;
        public bool AllowShake;
        public bool AllowFollowTarget;

        [Header("Zooming")]
        public float Zoom = 1f;
        public float ZoomSpeed = 150;
        public float ZoomSmoothing = 20;
        public float MinZoom = 30;
        public float MaxZoom = 500f;

        [Header("Zooming Tilt")]
        public float TiltMaxChange = -30;
        public float TiltStart = 150;
        public float TiltAngle = 65;

        [Header("Panning")]
        public float PanBuffer = .1f;
        public float PanSpeed = 20f;
        public float PanningMinX;
        public float PanningMinZ;
        public float PanningMaxX;
        public float PanningMaxZ;
        public float PanSmoothing = 20f;
        public float FollowSmoothing = 8f;
        public float MoveToTargetTime = 0.5f;

        [Header("Rotating")]
        public float RotationSpeed = 1;
        public float RotationSmoothing = 8;
        public float TiltSmoothing = 8;

        [Header("Other")]
        public float MinHeightOffset = 10;
        public Vector3 Panning { get; set; }
        public float Rotation { get; set; }

        public Vector3 TargetPosition { get; private set; }
        public float TargetZoom { get; private set; }
        public float3 TargetToFollow { get; set; }
        public bool NewTargetPosition { get; set; }
        public bool TargetPosMinimap { get; set; }
        public float ShakeIntensity { get; private set; }
        public float ShakeTime { get; set; }
        public float ZoomInput { get; set; }

        //----------------------------------------------------------------------------------------
        private int m_panBufferLeft;
        private int m_panBufferRight;
        private int m_pabBufferBottom;
        private int m_panBufferTop;

        //----------------------------------------------------------------------------------------
        public float XDistance {get { return Mathf.Cos(Mathf.Deg2Rad * (transform.rotation.eulerAngles.y - 90)) * Zoom; } }
        public float YDistance {get { return Zoom; } }
        public float ZDistance {get { return Mathf.Sin(Mathf.Deg2Rad * (transform.rotation.eulerAngles.y - 90)) * Zoom; } }

        //----------------------------------------------------------------------------------------
        public void Shake(Vector3 _position, float _time, float _intensity)
        {
            if(!AllowShake)
                return;

            if(!GeneralUtils.IsPointOnScreen(Camera.main, _position))
                return;

            ShakeTime = _time;
            ShakeIntensity = _intensity;
        }


        //----------------------------------------------------------------------------------------
        public void MoveTo(Vector3 _position, float _zoom = -1, bool _immediately = false, bool _minimap = false, bool _force = false)
        {
            if (!AllowMoveTo && ! _force)
                return;

            Vector3 pos = _position;
            pos.y = 0;

            TargetPosMinimap = _minimap;
            if (_immediately)
            {
                Target.transform.position = pos;
                Zoom = _zoom;
            }
            else
            {
                TargetPosition = pos;
                TargetZoom = _zoom;
                NewTargetPosition = true;
            }
        }

        //----------------------------------------------------------------------------------------
        public void FollowTarget(float3 _target)
        {
            if(!AllowFollowTarget)
                return;

            TargetToFollow = _target;
        }

        //----------------------------------------------------------------------------------------
        public int Zooming()
        {
            int zooming = NO_ZOOM;
            if(EventSystem.current.IsPointerOverGameObject())
                return zooming;

            ZoomInput = 0;
            if (AllowWheelZooming)
            {
                float wheelZoom = Input.GetAxis("Mouse ScrollWheel");
                if (!wheelZoom.Equals(0f))
                    zooming |= ZOOM_WHEEL;

                ZoomInput -= wheelZoom;
            }

            if (AllowKeyZooming)
            {
                float keyZoom = Input.GetKey(KeyCode.R) ? 0.05f : 0f;
                keyZoom += Input.GetKey(KeyCode.F) ? -0.05f : 0f;
                if (!keyZoom.Equals(0f))
                    zooming |= ZOOM_KEY;
                ZoomInput += keyZoom;
            }
            return Math.Abs(ZoomInput) > 0.00001f?zooming:NO_ZOOM;
        }

        //----------------------------------------------------------------------------------------
        public int PanEdge()
        {
            int pan = 0;
            if(!EdgePanningValid())
                return pan;

            if (Input.mousePosition.x < m_panBufferLeft)
                pan |= PAN_LEFT;
            if(Input.mousePosition.x > m_panBufferRight)
                pan |= PAN_RIGHT;
            if(Input.mousePosition.y < m_pabBufferBottom)
                pan |= PAN_DOWN;
            if(Input.mousePosition.y > m_panBufferTop)
                pan |= PAN_UP;

            return pan;
        }

        //----------------------------------------------------------------------------------------
        public int PanKey()
        {
            int pan = 0;
            if(!AllowKeyPanning)
                return pan;

            if(Input.GetAxis("Horizontal") < 0 || Input.GetKey(KeyCode.A))
                pan |= PAN_LEFT;
            if(Input.GetAxis("Horizontal") > 0 || Input.GetKey(KeyCode.S))
                pan |= PAN_RIGHT;
            if(Input.GetAxis("Vertical") < 0 || Input.GetKey(KeyCode.D))
                pan |= PAN_DOWN;
            if(Input.GetAxis("Vertical") > 0 || Input.GetKey(KeyCode.W))
                pan |= PAN_UP;

            return pan;
        }

        //----------------------------------------------------------------------------------------
        public bool PanLeftEdge() { return EdgePanningValid() && Input.mousePosition.x < m_panBufferLeft; }
        public bool PanRightEdge() { return EdgePanningValid() && Input.mousePosition.x > m_panBufferRight; }
        public bool PanDownEdge() { return EdgePanningValid() && Input.mousePosition.y < m_pabBufferBottom; }
        public bool PanUpEdge() { return EdgePanningValid() && Input.mousePosition.y > m_panBufferTop; }

        //----------------------------------------------------------------------------------------
        public bool PanLeft() { return AllowKeyPanning && (Input.GetAxis("Horizontal") < 0 || Input.GetKey(KeyCode.A)); }
        public bool PanRight() { return AllowKeyPanning && (Input.GetAxis("Horizontal") > 0 || Input.GetKey(KeyCode.S)); }
        public bool PanDown() { return AllowKeyPanning && (Input.GetAxis("Vertical") < 0 || Input.GetKey(KeyCode.D));}
        public bool PanUp() { return AllowKeyPanning && (Input.GetAxis("Vertical") > 0 || Input.GetKey(KeyCode.W)); }

        //----------------------------------------------------------------------------------------
        public bool MouseDragPanning()
        {
            if (!AllowMouseDragPanning)
                return false;

            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            return Input.GetMouseButton(MouseButtons.MIDDLE) && (Math.Abs(mouseX) > Mathf.Epsilon || Math.Abs(mouseY) > Mathf.Epsilon);
        }

        //----------------------------------------------------------------------------------------
        public bool RotateLeft()
        {
            if (!AllowMouseRotation)
                return false;

            return Input.GetMouseButton(MouseButtons.LEFT) && (Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.LeftShift));
        }

        //----------------------------------------------------------------------------------------
        public bool RotateRight()
        {
            if(!AllowMouseRotation)
                return false;

            return Input.GetMouseButton(MouseButtons.RIGHT) && (Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.LeftShift));
        }

        //----------------------------------------------------------------------------------------
        private void Start()
        {
            StoreTarget();

            m_panBufferLeft = Mathf.RoundToInt(Screen.width * PanBuffer);
            m_panBufferRight = Mathf.RoundToInt(Screen.width - (Screen.width * PanBuffer));
            m_pabBufferBottom = Mathf.RoundToInt(Screen.height * PanBuffer);
            m_panBufferTop = Mathf.RoundToInt(Screen.height - (Screen.height * PanBuffer));
            Rotation = 0;

            var rtsCamera = GetComponent<RTSCamera>();
            if (rtsCamera == null)
                gameObject.AddComponent<RTSCamera>();
        }

        //----------------------------------------------------------------------------------------
        private void StoreTarget()
        {
            if(transform.parent == null)
            {
                Target = new GameObject {name = "CameraTarget"};
                Target.transform.position = StartPosition;
                transform.parent = Target.transform;
            }
            else {
                Target = transform.parent.gameObject;
            }
        }

        //----------------------------------------------------------------------------------------
        private bool EdgePanningValid()
        {
            if(!AllowEdgePanning)
                return false;

            if(EventSystem.current.IsPointerOverGameObject())
                return false;

            if(Input.GetMouseButton(MouseButtons.MIDDLE))
                return false;

            return true;
        }

    }
}