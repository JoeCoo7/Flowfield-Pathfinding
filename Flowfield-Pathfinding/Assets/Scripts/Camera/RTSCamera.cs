using UnityEngine;
using WT.ECS;
using Math = System.Math;

//----------------------------------------------------------------------------------------
namespace WT
{
    //----------------------------------------------------------------------------------------
    public class RTSCamera : MonoBehaviour
    {
        private const int POSITION_CHANGE_MOUSEEDGE = 1 << 0;
        private const int POSITION_CHANGE_ARROWKEY = 1 << 1;
        private const int POSITION_CHANGE_MOUSEPAN = 1 << 2;
        private const int ZOOM_CHANGE_MOUSE = 1 << 3;
        private const int ZOOM_CHANGE_KEY = 1 << 4;
        private const int POSITION_CHANGE_MINIMAP = 1 << 5;
        private const int POSITION_CHANGE_MOVETO = 1 << 6;

        private RTSCameraSettings m_settings;
        private float m_smoothTitlt;
        private float m_smoothVelocity;

        private Vector3 m_lastMousePosition;
        private float m_zoom;
        private float m_rotation;
        private float m_zoomVelocity;
        private float m_rotationVelocity;
        private float m_followVeloctiyX;
        private float m_followVeloctiyZ;
        private float m_currentShakeTime;
        private bool m_followingTarget;

        private Vector3 m_lastPosition;
        private float m_lastZoom;
        private float m_lastRotiation;
        private byte m_cameraChangeFlags;

        //----------------------------------------------------------------------------------------
        private void Start()
        {
            m_settings = GetComponent<RTSCameraSettings>();
            m_lastZoom = m_zoom = m_settings.Zoom;
            m_lastRotiation = m_rotation = m_settings.Rotation;
            m_lastPosition = m_settings.StartPosition;
            m_cameraChangeFlags = 0;
        }

        //----------------------------------------------------------------------------------------
        public void ResetChangeValues()
        {
            m_cameraChangeFlags = 0;
            m_lastPosition = m_settings.Target.transform.position;
            m_lastZoom = m_zoom;
            m_lastRotiation = m_rotation;
        }

        //----------------------------------------------------------------------------------------
        public bool PositionChanged()
        {
            bool changed = !m_lastPosition.Equals(m_settings.Target.transform.position);
            return changed;
        }

        //----------------------------------------------------------------------------------------
        public bool ZoomChanged()
        {
            bool changed = !m_lastZoom.Equals(m_zoom);
            return changed;
        }

        public int GetChangeFlags() {  return m_cameraChangeFlags;}

        //----------------------------------------------------------------------------------------
        public bool RotationChanged()
        {
            bool changed = !m_lastRotiation.Equals(m_rotation);
            return changed;
        }

        //----------------------------------------------------------------------------------------
        private void LateUpdate()
        {
            UpdatePanning();
            UpdateZooming();
            UpdateRotation();
            UpdateShake();
            UpdateGotoTarget();
            UpdateFollowTarget();
            Apply();
        }

        //----------------------------------------------------------------------------------------
        private void OnDestroy()
        {
//            if(m_targetTween != null)
//                m_targetTween.Kill();
//            if(m_targetZoomTween != null)
//                m_targetZoomTween.Kill();
        }

        //----------------------------------------------------------------------------------------
        private void UpdatePanning()
        {
            int pan = m_settings.PanEdge();
            Vector3 edgePan = GetPanning(pan);
            if(!edgePan.Equals(Vector3.zero))
                m_cameraChangeFlags |= POSITION_CHANGE_MOUSEEDGE;

            pan = m_settings.PanKey();
            Vector3 keyPan = GetPanning(pan);
            if(!keyPan.Equals(Vector3.zero))
                m_cameraChangeFlags |= POSITION_CHANGE_ARROWKEY;

            m_settings.Panning += keyPan + edgePan;
            UpdateMouseDragPanning();
            m_lastMousePosition = Input.mousePosition;
            Clamp();
        }

        //----------------------------------------------------------------------------------------
        private Vector3 GetPanning(int _panFlags)
        {
            Vector3 panPos = Vector3.zero;
            if(_panFlags == 0)
                return panPos;

            if((_panFlags & RTSCameraSettings.PAN_LEFT) != 0)
            {
                panPos.x += m_settings.PanSpeed * -transform.right.x;
                panPos.z += m_settings.PanSpeed * -transform.right.z;
            }
            else if((_panFlags & RTSCameraSettings.PAN_RIGHT) != 0)
            {
                panPos.x += m_settings.PanSpeed * transform.right.x;
                panPos.z += m_settings.PanSpeed * transform.right.z;
            }

            if((_panFlags & RTSCameraSettings.PAN_UP) != 0)
            {
                panPos.x += m_settings.PanSpeed * transform.forward.x;
                panPos.z += m_settings.PanSpeed * transform.forward.z;
            }
            else if((_panFlags & RTSCameraSettings.PAN_DOWN) != 0)
            {
                panPos.x += m_settings.PanSpeed * -transform.forward.x;
                panPos.z += m_settings.PanSpeed * -transform.forward.z;
            }

            return panPos;
        }
        
        //----------------------------------------------------------------------------------------
        private void UpdateMouseDragPanning()
        {
            if(!m_settings.MouseDragPanning())
                return;

            BreakTargetFollow();

            RaycastHit curHitInfo;
            if(!Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out curHitInfo, Mathf.Infinity))
                return;

            RaycastHit lastHitInfo;
            if(!Physics.Raycast(Camera.main.ScreenPointToRay(m_lastMousePosition), out lastHitInfo, Mathf.Infinity))
                return;

            Vector3 delta = (curHitInfo.point - lastHitInfo.point);
            delta *= -1;

            if (!delta.Equals(Vector3.zero))
                m_cameraChangeFlags |= POSITION_CHANGE_MOUSEPAN;

            m_settings.Panning += delta;
        }

        //----------------------------------------------------------------------------------------
        private void UpdateZooming()
        {
            int zoom = m_settings.Zooming();
            if (zoom != RTSCameraSettings.NO_ZOOM)
            {
                if((zoom & RTSCameraSettings.ZOOM_WHEEL) != 0)
                    m_cameraChangeFlags |= ZOOM_CHANGE_MOUSE;

                if((zoom & RTSCameraSettings.ZOOM_KEY) != 0)
                    m_cameraChangeFlags |= ZOOM_CHANGE_KEY;

                m_zoom += m_settings.ZoomInput * m_settings.ZoomSpeed;
                m_zoom = Mathf.Max(Mathf.Min(m_zoom, m_settings.MaxZoom), m_settings.MinZoom);
            }
            m_settings.Zoom = Time.deltaTime>0 ? Mathf.SmoothDamp(m_settings.Zoom, m_zoom, ref m_zoomVelocity, Time.deltaTime * m_settings.ZoomSmoothing) : m_zoom;
        }

        //----------------------------------------------------------------------------------------
        private void Clamp()
        {
            Vector3 pos = m_settings.Panning;
            pos.x = Math.Min(Math.Max(pos.x, m_settings.PanningMinZ), m_settings.PanningMaxZ);
            pos.z = Math.Min(Math.Max(pos.z, m_settings.PanningMinX), m_settings.PanningMaxX);
            pos.y = 0;
            m_settings.Panning = pos;
        }

        //----------------------------------------------------------------------------------------
        private void UpdateFollowTarget()
        {
            if (!m_followingTarget)
                return;

            m_followingTarget = true;
            var pos = m_settings.Panning;

            if (Time.deltaTime > 0)
            {
                pos.x = Mathf.SmoothDamp(pos.x, m_settings.TargetToFollow.x, ref m_followVeloctiyX, Time.deltaTime * m_settings.FollowSmoothing);
                pos.z = Mathf.SmoothDamp(pos.z, m_settings.TargetToFollow.z, ref m_followVeloctiyZ, Time.deltaTime * m_settings.FollowSmoothing);
            }
            else
            {
                pos.x = m_settings.TargetToFollow.x;
                pos.z = m_settings.TargetToFollow.z;
            }
            m_settings.Panning = pos;
            m_settings.Panning = pos;
        }

        //----------------------------------------------------------------------------------------
        private void UpdateShake()
        {
            if (m_currentShakeTime <= 0)
            {
                m_currentShakeTime = m_settings.ShakeTime;
                m_settings.ShakeTime = 0;
                return;
            }

            m_currentShakeTime -= Time.deltaTime;
            float percentComplete = m_currentShakeTime / m_settings.ShakeTime;
            float damper = 1.0f - Mathf.Clamp(4.0f * percentComplete - 3.0f, 0.0f, 1.0f);
            float x = Random.value * 2.0f - 1.0f;
            float z = Random.value * 2.0f - 1.0f;
            x *= m_settings.ShakeIntensity * damper;
            z *= m_settings.ShakeIntensity * damper;
            m_settings.Panning += new Vector3(x, 0, z);
        }

        //----------------------------------------------------------------------------------------
        private void UpdateGotoTarget()
        {
        }

        //----------------------------------------------------------------------------------------
        private void UpdateRotation()
        {
            if(m_settings.RotateLeft())
                m_rotation -= m_settings.RotationSpeed;
            else if(m_settings.RotateRight())
                m_rotation += m_settings.RotationSpeed;

            m_settings.Rotation = Time.deltaTime > 0 ?  Mathf.SmoothDamp(m_settings.Rotation, m_rotation, ref m_rotationVelocity, Time.deltaTime * m_settings.RotationSmoothing) : m_rotation;
        }

        //----------------------------------------------------------------------------------------
        private void BreakTargetFollow()
        {
            m_followingTarget = false;
        }

        //----------------------------------------------------------------------------------------
        private void Apply()
        {
            m_settings.Target.transform.position = Time.deltaTime > 0 ? Vector3.Lerp(m_settings.Target.transform.position, m_settings.Panning, m_settings.PanSmoothing * Time.deltaTime) : m_settings.Panning;

            Vector3 currentPos = m_settings.Target.transform.position;
            currentPos.x += m_settings.XDistance;
            currentPos.y += m_settings.YDistance;
            currentPos.z += m_settings.ZDistance;

            float target = m_settings.TiltStart;
            if (m_settings.AllowZoomTilting)
            {
                float zoomChange = m_settings.TiltMaxChange / (m_settings.TiltStart - Mathf.Abs(m_settings.MinZoom));
                float change = Math.Min((m_settings.TiltStart - m_settings.Zoom) * zoomChange, 0);
                target = m_settings.TiltAngle + change;
            }

            m_smoothTitlt = Time.deltaTime > 0 ? Mathf.SmoothDamp(m_smoothTitlt, target, ref m_smoothVelocity, Time.deltaTime * m_settings.TiltSmoothing) : target;
            Quaternion rotation = Quaternion.Euler(m_smoothTitlt, m_settings.Rotation, 0);
            Vector3 position = rotation * new Vector3(0, 0, -currentPos.y) + m_settings.Target.transform.position;
            position.y = Math.Max(position.y, m_settings.MinHeightOffset);

            transform.position = Time.deltaTime > 0 ? Vector3.Lerp(transform.position, position, m_settings.PanSmoothing * Time.deltaTime) : position;
            transform.rotation = rotation;
        }
    }
}