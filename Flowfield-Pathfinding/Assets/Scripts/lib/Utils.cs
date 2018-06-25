using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

//-----------------------------------------------------------------------------
namespace RSGLib.utility
{

    //-----------------------------------------------------------------------------
    public static class Utils
    {
        private static readonly PointerEventData EVENT_DATA_CURRENT_POSITION = new PointerEventData(EventSystem.current);
        private static readonly List<RaycastResult> RESULTS = new List<RaycastResult>();
        private static Rect m_screenRect = new Rect(0f, 0f, Screen.width, Screen.height);

        //-----------------------------------------------------------------------------
        public static float DeltaTime
        {
            get
            {
                if (System.Math.Abs(Time.timeScale) > MathLib.TOLERANCE)
                    return Time.deltaTime/Time.timeScale;

                return 1.0f/Time.captureFramerate;
            }
        }


        //-----------------------------------------------------------------------------
        public static float SmoothDeltaTime
        {
            get
            {
                if (System.Math.Abs(Time.timeScale) > MathLib.TOLERANCE)
                    return Time.smoothDeltaTime/Time.timeScale;

                return 1.0f/Time.captureFramerate;
            }
        }

        //-----------------------------------------------------------------------------
        public static T InstantiateFromResource<T>(string _name, Transform _parent = null, string _newName = "") where T : Component
        {
            GameObject gameObject = Object.Instantiate(Resources.Load(_name)) as GameObject;
            if (gameObject == null)
                throw new ApplicationException(_name + " prefab not found");

            T component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = gameObject.GetComponentInChildren<T>();
                if (component == null)
                    throw new ApplicationException("component not found in " + _name + " prefab");
            }

            if (_newName != "")
                gameObject.name = _newName;
            else
                gameObject.name = _name.Substring(_name.LastIndexOf("/", StringComparison.Ordinal) + 1);
            
            if (_parent)
                gameObject.transform.SetParent(_parent, false);

            return component;
        }

        //-----------------------------------------------------------------------------
        public static T InstantiateAssetFromResource<T>(string _name, string _newName = "") where T : ScriptableObject
        {
            T objectData = Resources.Load(_name) as T;
            if (objectData == null)
                throw new ApplicationException(_name + " scriptable object not found");

            if (_newName != "")
                objectData.name = _newName;
            else
                objectData.name = _name.Substring(_name.LastIndexOf("/", StringComparison.Ordinal) + 1);

            return objectData;
        }
        
        //-----------------------------------------------------------------------------
        public static GameObject InstantiateFromResource(string _name, Transform _parent = null, string _newName = "")
        {
            GameObject gameObject = Object.Instantiate(Resources.Load(_name)) as GameObject;
            if (gameObject == null)
                throw new ApplicationException(_name + " prefab not found");

            if (_newName != "")
                gameObject.name = _newName;
            else 
                gameObject.name = _name.Substring(_name.LastIndexOf("/", StringComparison.Ordinal) + 1);
            

            if (_parent)
                gameObject.transform.SetParent(_parent, false);

            return gameObject;
        }


        //-----------------------------------------------------------------------------
        public static void Shuffle<T>(this IList<T> _list)
        {
            int n = _list.Count;
            while (n > 1)
            {
                n--;
                int k = Random.Range(0, n + 1);
                T value = _list[k];
                _list[k] = _list[n];
                _list[n] = value;
            }
        }

        //-----------------------------------------------------------------------------
        public static bool IsPointerOverUIObject()
        {
            EVENT_DATA_CURRENT_POSITION.position = Input.mousePosition;
            EventSystem.current.RaycastAll(EVENT_DATA_CURRENT_POSITION, RESULTS);
            return RESULTS.Count > 0;
        }

        //-----------------------------------------------------------------------------
        public static int IntParseFast(string _value)
        {
            int result = 0;
            for (int count = 0; count < _value.Length; count++)
                result = 10*result + (int) char.GetNumericValue(_value[count]);

            return result;
        }

        //-----------------------------------------------------------------------------
        public static string FormatSeconds(double _time)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(_time);
            return string.Format("{0:D2}:{1:D2}", timeSpan.Minutes, timeSpan.Milliseconds);
        }

        //-----------------------------------------------------------------------------
        public static string FormatMinutes(double _time)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(_time);
            return string.Format("{0:D2}:{1:D2}", timeSpan.Minutes, timeSpan.Seconds);
        }

        //-----------------------------------------------------------------------------
        public static string FormatHours(double _time)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(_time);
            return string.Format("{0:D2}:{1:D2}:{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
        }

        //-----------------------------------------------------------------------------
        //http://stackoverflow.com/questions/7040289/converting-integers-to-roman-numerals
        public static string ToRoman(int _number)
        {
            if ((_number < 0) || (_number > 3999))
                throw new ArgumentOutOfRangeException("insert value betwheen 1 and 3999");

            if (_number < 1) return string.Empty;
            if (_number >= 1000) return "M" + ToRoman(_number - 1000);
            if (_number >= 900) return "CM" + ToRoman(_number - 900);
            if (_number >= 500) return "D" + ToRoman(_number - 500);
            if (_number >= 400) return "CD" + ToRoman(_number - 400);
            if (_number >= 100) return "C" + ToRoman(_number - 100);
            if (_number >= 90) return "XC" + ToRoman(_number - 90);
            if (_number >= 50) return "L" + ToRoman(_number - 50);
            if (_number >= 40) return "XL" + ToRoman(_number - 40);
            if (_number >= 10) return "X" + ToRoman(_number - 10);
            if (_number >= 9) return "IX" + ToRoman(_number - 9);
            if (_number >= 5) return "V" + ToRoman(_number - 5);
            if (_number >= 4) return "IV" + ToRoman(_number - 4);
            if (_number >= 1) return "I" + ToRoman(_number - 1);
            throw new ArgumentOutOfRangeException("something bad happened");
        }

        //-----------------------------------------------------------------------------
        public static string ColorToHex(Color32 _color, bool _includeAlpha = true)
        {
            string r = _color.r.ToString("X2");
            string g = _color.g.ToString("X2");
            string b = _color.b.ToString("X2");
            string hexColor = "#" + r + g + b;
            if (_includeAlpha)
                hexColor += _color.a.ToString("X2");

            return hexColor;
        }

        //-----------------------------------------------------------------------------
        public static Color32 HexToColor(long _hexVal)
        {
            byte R = (byte) ((_hexVal >> 16) & 0xFF);
            byte G = (byte) ((_hexVal >> 8) & 0xFF);
            byte B = (byte) ((_hexVal) & 0xFF);
            return new Color32(R, G, B, 255);
        }

        //----------------------------------------------------------------------------------------
        public static void ResetItemTransform(Transform _transform)
        {
            _transform.transform.localScale = Vector3.one;
            _transform.transform.localPosition = Vector3.zero;
            _transform.transform.localRotation = Quaternion.identity;
        }

        //----------------------------------------------------------------------------------------
        public static void DestroyChildren(Transform _transform)
        {
            for (int i = _transform.childCount - 1; i >= 0; --i)
                GameObject.Destroy(_transform.GetChild(i).gameObject);

            _transform.DetachChildren();
            Resources.UnloadUnusedAssets();
        }

        //----------------------------------------------------------------------------------------
        public static void DestroyChildrenEditor(Transform _transform)
        {
            for (int i = _transform.childCount - 1; i >= 0; --i)
                GameObject.DestroyImmediate(_transform.GetChild(i).gameObject);

            _transform.DetachChildren();
            Resources.UnloadUnusedAssets();
        }

        //----------------------------------------------------------------------------------------
        public static bool IsRectTransformOnScreen(RectTransform _rectTransform)
        {
            Vector3[] objectCorners = new Vector3[4];
            _rectTransform.GetWorldCorners(objectCorners);

            foreach (Vector3 corner in objectCorners)
            {
                if (m_screenRect.Contains(corner) == false)
                    return false;
            }
            return true;
        }

        //-----------------------------------------------------------------------------
        public static Transform FindObject<T>(Transform _transform) where T : Component
        {
            if (_transform == null)
                return null;

            T component = _transform.GetComponent<T>();
            return component == null ? FindObject<T>(_transform.parent) : component.transform;
        }

        //-----------------------------------------------------------------------------
        public static void SetLayerRecursively(this GameObject _obj, int _layer)
        {
            _obj.layer = _layer;
            foreach (Transform child in _obj.transform)
                child.gameObject.SetLayerRecursively(_layer);
        }

        //-----------------------------------------------------------------------------
        public static bool IsPointOnScreen(Camera _camera, Vector3 _position)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(_camera);
            foreach(var plane in planes)
            {
                if(plane.GetDistanceToPoint(_position) < 0)
                    return false;
            }
            return true;
        }

        //-----------------------------------------------------------------------------
        public static Texture2D SpriteToTexture(Sprite _sprite)
        {
            Rect spriteRect = _sprite.textureRect;
            Texture2D texture = new Texture2D((int) spriteRect.width, (int)spriteRect.height);
            var pixels = _sprite.texture.GetPixels((int)spriteRect.x, (int)spriteRect.y, (int)spriteRect.width, (int)spriteRect.height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
