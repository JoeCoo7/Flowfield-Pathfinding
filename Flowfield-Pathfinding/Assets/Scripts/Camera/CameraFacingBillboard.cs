using UnityEngine;
using WT;

//----------------------------------------------------------------------------------------
namespace WT.ECS
{
    //----------------------------------------------------------------------------------------
    public class CameraFacingBillboard : MonoBehaviour
    {
        public enum AxisEnum { Up, Down, Left, Right, Forward, Back };
        public bool ReverseFace = false;
        public bool ScaleWithCamera = false;
        public float MinScale = 1;
        public float MaxScale = 2;
        public AxisEnum Axis = AxisEnum.Up;
        private Camera m_camera;
        private RTSCameraSettings m_cameraSettings;


        //----------------------------------------------------------------------------------------
        private void Awake()
        {
            m_camera = Camera.main;
            m_cameraSettings = m_camera.GetComponent<RTSCameraSettings>();
        }

        //----------------------------------------------------------------------------------------
        public Vector3 GetAxis(AxisEnum _refAxisEnum)
        {
            switch (_refAxisEnum)
            {
                case AxisEnum.Down:return Vector3.down;
                case AxisEnum.Forward:return Vector3.forward;
                case AxisEnum.Back:return Vector3.back;
                case AxisEnum.Left:return Vector3.left;
                case AxisEnum.Right:return Vector3.right;
                default:return Vector3.up;
            }
        }

        //----------------------------------------------------------------------------------------
        private void LateUpdate()
        {
            Vector3 targetPos = transform.position + m_camera.transform.rotation * (ReverseFace ? Vector3.forward : Vector3.back);
            Vector3 targetOrientation = m_camera.transform.rotation * GetAxis(Axis);
            transform.LookAt(targetPos, targetOrientation);

            
            float scale = ((m_cameraSettings.Zoom + Mathf.Abs(m_cameraSettings.MinZoom)) / m_cameraSettings.MaxZoom) * MaxScale + MinScale;
            scale = Mathf.Min(scale, MaxScale);
            transform.localScale = Vector3.one * scale; 
        }
    }
} 