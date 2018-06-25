using UnityEngine;

//-----------------------------------------------------------------------------
namespace RSGLib
{
	//-----------------------------------------------------------------------------
	public static class CameraExtensions
    {
		//-----------------------------------------------------------------------------
		public static Rect OrthographicBounds(this Camera _camera)
        {
            float screenAspect = (float)Screen.width / Screen.height;
            float cameraHeight = _camera.orthographicSize * 2;
            float cameraWidth = cameraHeight*screenAspect;
            Rect bounds = new Rect(_camera.transform.position.x - cameraWidth * 0.5f, _camera.transform.position.y - cameraHeight * 0.5f, cameraWidth, cameraHeight);
            return bounds;
        }

        //-----------------------------------------------------------------------------
	    public static Vector3 ScreenToWorldPoint3D(this Camera _camera, Vector3 _screenPos)
	    {
            Vector3 pos = _screenPos;
	        pos.z = -_camera.transform.position.z;
            return _camera.ScreenToWorldPoint(pos);
	    }

        //-----------------------------------------------------------------------------
        public static Bounds GetViewportBounds(this Camera _camera, Vector3 _screenPosition1, Vector3 _screenPosition2)
        {
            var v1 = _camera.ScreenToViewportPoint(_screenPosition1);
            var v2 = _camera.ScreenToViewportPoint(_screenPosition2);
            var min = Vector3.Min(v1, v2);
            var max = Vector3.Max(v1, v2);
            min.z = _camera.nearClipPlane;
            max.z = _camera.farClipPlane;
            //min.z = 0.0f;
            //max.z = 1.0f;

            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

    }
}
