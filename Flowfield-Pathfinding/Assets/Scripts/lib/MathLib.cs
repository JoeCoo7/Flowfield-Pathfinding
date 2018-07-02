//-----------------------------------------------------------------------------
// File: Math.cs
//
// Basic math functions
//
// Copyright (c) Matthias Schindler
//-----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

//-----------------------------------------------------------------------------
namespace RSGLib.utility
{
	//-----------------------------------------------------------------------------
    public static class MathLib
    {
        public static readonly float TOLERANCE = 0.001f;
        public static readonly float HALF_PI = Mathf.PI*0.5f;
        public static readonly string[] SUFFIXES =  {"", "K", "M", "B", "T", "Q", "Sx","Sp", "O", "N", "D", "U", "Duo", "Tre", "Quat", "Quin", "Sex", "Sept", "Oct","Noem", "Vig", "OMG" };
        public static readonly string[] LONG_SUFFIXES = { "", "K", "Million", "Billion", "Trillion", "Quadrillion", "Sextillion", "Septillion", "Octillian", "Nonillion", "Decillion", "Undecillion",
                                                         "Duodecillion", "Tredeclllion", "Quattuordecillion", "Quindecillion", "Sexdeclillion", "Septendecillion", "Octodeclllion", "Noemdecillion", "Vigintillion", "OMGWTF"};

        private static Vector2 m_box1 = new Vector2();
        private static Vector2 m_box2 = new Vector2();
        private static Vector2 m_cacheVec = new Vector2();
        private static Vector3 m_planarTarget = new Vector3();
        private static Vector3 m_planarPos = new Vector3();
        private static Vector3 m_velocity = new Vector3();

        //-----------------------------------------------------------------------------
        public static bool Intersect(Rect _a, Rect _b)
        {
            FlipNegative(ref _a);
            FlipNegative(ref _b);
            bool c1 = _a.xMin < _b.xMax;
            bool c2 = _a.xMax > _b.xMin;
            bool c3 = _a.yMin < _b.yMax;
            bool c4 = _a.yMax > _b.yMin;
            return c1 && c2 && c3 && c4;
        }

        //-----------------------------------------------------------------------------
        public static bool LineIntersect(Vector2 _a, Vector2 _b, Vector2 _c, Vector2 _d)
		{
			int test1A = CheckTriClockDir( _a, _b, _c );
			int test1B = CheckTriClockDir( _a, _b, _d );
		    if (test1A == test1B) 
                return false;

		    int test2A = CheckTriClockDir( _c, _d, _a );
		    int test2B = CheckTriClockDir( _c, _d, _b );
		    return test2A != test2B;
		}

        //-----------------------------------------------------------------------------
        public static bool RayBoxIntersect(Vector2 _r1, Vector2 _r2, Rect _box) 
		{
			m_box1.Set(_box.x, _box.y);
			m_box2.Set(_box.x + _box.width, _box.y + _box.height);
			
			m_cacheVec.Set(m_box2.x, m_box1.y);
			if (RayRayIntersect(_r1, _r2, m_box1, m_cacheVec))
				return true;
			
			m_cacheVec.Set(m_box1.x, m_box2.y);				
			if (RayRayIntersect(_r1, _r2, m_box1, m_cacheVec))
				return true;
			
			m_cacheVec.Set(m_box2.x, m_box1.y);				
			if (RayRayIntersect(_r1, _r2, m_box2, m_cacheVec))
				return true;
			
			m_cacheVec.Set(m_box1.x, m_box2.y);			
	
			return RayRayIntersect(_r1, _r2, m_box2, m_cacheVec);
		}

        //-----------------------------------------------------------------------------
        public static bool RayRayIntersect(Vector2 _p1, Vector2 _p2, Vector2 _p3, Vector2 _p4)
		{
			float denom = ((_p4.y - _p3.y)*(_p2.x - _p1.x)) - ((_p4.x - _p3.x)*(_p2.y - _p1.y));
			float numeA  = ((_p4.x - _p3.x)*(_p1.y - _p3.y)) - ((_p4.y - _p3.y)*(_p1.x - _p3.x));
			float numeB  = ((_p2.x - _p1.x)*(_p1.y - _p3.y)) - ((_p2.y - _p1.y)*(_p1.x - _p3.x));
	
			if(System.Math.Abs(denom) < TOLERANCE) {
				if(System.Math.Abs(numeA) < TOLERANCE && System.Math.Abs(numeB) < TOLERANCE) {
					return false; //COINCIDENT;
				}
				return false; //PARALLEL;
			}
	
			float ua = numeA / denom;
			float ub = numeB / denom;
	  
			return (ua >= 0.0f) && (ua <= 1.0f) && (ub >= 0.0f) && (ub <= 1.0f);
		}		
		


        //-----------------------------------------------------------------------------
		private static int CheckTriClockDir(Vector2 _p1, Vector2 _p2, Vector2 _p3)
		{
			float test = ((( _p2.x - _p1.x )*( _p3.y - _p1.y )) - (( _p3.x - _p1.x)*( _p2.y - _p1.y )));
            if(test > 0) 
                return 1;
			if(test < 0) 
                return -1;

			return 0;
		}


        //-----------------------------------------------------------------------------
        public static void FlipNegative(ref Rect _r)
        {
            if (_r.width < 0)
                _r.x -= (_r.width *= -1);
            if (_r.height < 0)
                _r.y -= (_r.height *= -1);
        }


        //-----------------------------------------------------------------------------
        public static float Mod(float _a, float _b)
        {
            return System.Math.Abs(_b) > TOLERANCE ? _a%_b : 0.0f;
        }

        //-----------------------------------------------------------------------------
        public static float Wrap(float _current, float _min, float _max) // [min .. max)
        {
            var x = Mod(_current - _min, _max - _min);

            return x < 0.0f ? _max + x : _min + x;
        }

        //-----------------------------------------------------------------------------
        public static float Delta(float _current, float _target, float _min, float _max)
        {
            _current = Wrap(_current, _min, _max);
            _target = Wrap(_target, _min, _max);

            float r = _max - _min;
            float h = r/2.0f;
            float d = _target - _current;

            if (d > h)
                d -= r;
            else if (d < -h)
                d += r;

            return d;
        }

        //-----------------------------------------------------------------------------
        public static bool IsEven(int _value)
        {
            return (_value & 1) == 0;
        }

        //-----------------------------------------------------------------------------
        public static bool IsPowerOfTwo(int _value)
        {
            return ((_value != 0) && ((_value & (~_value + 1)) == _value));
        }

        //-----------------------------------------------------------------------------
        public static bool IsInRange(int _value, int _min, int _max)
        {
            return (_value >= _min && _value <= _max);
        }
        //-----------------------------------------------------------------------------
        public static bool IsInRange(float _value, float _min, float _max)
        {
            return (_value >= _min && _value <= _max);
        }

        //-----------------------------------------------------------------------------
        public static bool IsInRangeExclude(int _value, int _min, int _max)
        {
            return (_value > _min && _value < _max);
        }

        //-----------------------------------------------------------------------------
		public static float DistanceSquared(float _x1,float _y1,float _x2, float _y2)
		{
			float distX = _x1 - _x2;
			float distY = _y1 - _y2;
			return distX * distX + distY * distY;
		}

        //-----------------------------------------------------------------------------
		public static float Distance(float _x1,float _y1,float _x2, float _y2)
		{
			float distX = _x1 - _x2;
			float distY = _y1 - _y2;
			return Mathf.Sqrt(distX * distX + distY * distY);
		}
		
        //-----------------------------------------------------------------------------
        public static float DistanceManhatten(float _x1, float _y1, float _x2, float _y2)
		{
			return Mathf.Abs(_x1 - _x2) + Mathf.Abs(_y1 - _y2);		
		}

        //-----------------------------------------------------------------------------
        public static float DistanceManhatten(Vector2 _vec1, Vector2 _vec2)
        {
            return Mathf.Abs(_vec1.x - _vec2.x) + Mathf.Abs(_vec1.y - _vec2.y);
        }

        //-----------------------------------------------------------------------------
        public static float DistanceManhatten(Vector3 _vec1, Vector3 _vec2)
        {
            return Mathf.Abs(_vec1.x - _vec2.x) + Mathf.Abs(_vec1.y - _vec2.y) + Mathf.Abs(_vec1.z - _vec2.z);
        }

       //-----------------------------------------------------------------------------
        public static Vector2 PointOnCircle(float _radius, float _angleInDegrees, Vector2 _origin)
        {
            m_cacheVec.x = _radius * Mathf.Cos(_angleInDegrees * Mathf.Deg2Rad) + _origin.x;
            m_cacheVec.y = _radius * Mathf.Sin(_angleInDegrees * Mathf.Deg2Rad) + _origin.y;
            return m_cacheVec;
        }

        //-----------------------------------------------------------------------------
        //http://stackoverflow.com/questions/481144/equation-for-testing-if-a-point-is-inside-a-circle/481150#481150
        public static bool InCircle(Vector2 _circleOffset, float _radius, Vector2 _point)
        {
            float dx = Mathf.Abs(_point.x - _circleOffset.x);
            if(dx > _radius)
                return false;

            float dy = Mathf.Abs(_point.y - _circleOffset.y);
            if(dy > _radius)
                return false;

            if(dx + dy <= _radius)
                return true;

            return dx * dx + dy * dy <= _radius * _radius;
        }

        //-----------------------------------------------------------------------------
        public static bool InCircle(Vector3 _circleOffset, float _radius, Vector3 _point)
        {
            float dx = Mathf.Abs(_point.x - _circleOffset.x);
            if(dx > _radius)
                return false;

            float dz = Mathf.Abs(_point.z - _circleOffset.z);
            if(dz > _radius)
                return false;

            if(dx + dz <= _radius)
                return true;

            return dx * dx + dz * dz <= _radius * _radius;
        }

        //-----------------------------------------------------------------------------
        public static bool NotInCircle(Vector2 _circleOffset, float _radius, Vector2 _point)
        {
            float dx = Mathf.Abs(_point.x - _circleOffset.x);
            float dy = Mathf.Abs(_point.y - _circleOffset.y);
            return (dx * dx + dy * dy > _radius * _radius);
        }

        //-----------------------------------------------------------------------------
        public static bool NotInCircle(Vector3 _circleOffset, float _radius, Vector3 _point)
        {
            float dx = Mathf.Abs(_point.x - _circleOffset.x);
            float dz = Mathf.Abs(_point.z - _circleOffset.z);
            return (dx * dx + dz * dz > _radius * _radius);
        }

        //-----------------------------------------------------------------------------
        public static int TrimInt(int _value, int _min, int _max)
        {
            if (_value < _min)
                return _min;

            return _value > _max ? _max : _value;
        }

        //-----------------------------------------------------------------------------
        public static float TrimFloat(float _value, float _min, float _max)
        {
            if (_value < _min)
                return _min;

            return _value > _max ? _max : _value;
        }

        //-----------------------------------------------------------------------------
        public static Vector3 Abs(Vector3 _vector)
        {
            return new Vector3(Mathf.Abs(_vector.x), Mathf.Abs(_vector.y), Mathf.Abs(_vector.z));
        }

        //-----------------------------------------------------------------------------
        public static bool ContainsRect(this Rect _rect1, Rect _rect2)
        {
            return _rect1.Contains(new Vector2(_rect2.xMin, _rect2.yMin)) &&
                   _rect1.Contains(new Vector2(_rect2.xMax, _rect2.yMax));
        }

        //-----------------------------------------------------------------------------
        public static float CubicInterpolate(float _a, float _b, float _c, float _d, float _across)
        {
            float aSq = _across*_across;
            _d = (_d - _c) - (_a - _b);
            return _d*(aSq*_across) + ((_a - _b) - _d)*aSq + (_c - _a)*_across + _b;
        }

        //-----------------------------------------------------------------------------
        public static float CosineInterpolate(float _a, float _b, float _across)
        {
            _across = (1.0f - Mathf.Cos(_across*Mathf.PI))*0.5f;
            return _a*(1.0f - _across) + _b*_across;
        }

        //-----------------------------------------------------------------------------
        public static float DampenFactor(float _dampening, float _elapsed)
        {
            return 1.0f - Mathf.Pow((float) System.Math.E, -_dampening*_elapsed);
        }

        //-----------------------------------------------------------------------------
        public static float Frac(float _value)
        {
            return _value - (float) System.Math.Truncate(_value);
        }

        //-----------------------------------------------------------------------------
        public static float HatRandom(float _radius)
        {
            float area = 1*Mathf.Atan(1.0f);
            float p = area*Random.value;
            return Mathf.Tan(p/1)*_radius/1.0f;
        }

        //-----------------------------------------------------------------------------
        public static float LineRandom(float _range)
        {
            float area = (_range*_range)*0.5f;
            float p = area*Random.value;
            return _range - Mathf.Sqrt(_range*_range - 2*p);
        }

        //-----------------------------------------------------------------------------
        public static float Hermite(float _start, float _end, float _value)
        {
            return Mathf.Lerp(_start, _end, _value*_value*(3.0f - 2.0f*_value));
        }

        //-----------------------------------------------------------------------------
        public static Vector3 CatmullRom(Vector3 _previous, Vector3 _start, Vector3 _end, Vector3 _next, float _elapsedTime)
        {
            float percentComplete = _elapsedTime;
            float percentCompleteSquared = percentComplete*percentComplete;
            float percentCompleteCubed = percentCompleteSquared*percentComplete;
            return _previous*(-0.5F*percentCompleteCubed + percentCompleteSquared - 0.5f*percentComplete) + _start*
                   (1.5f*percentCompleteCubed + -2.5f*percentCompleteSquared + 1.0f) + _end*
                   (-1.5f*percentCompleteCubed + 2.0f*percentCompleteSquared + 0.5f*percentComplete) + _next*
                   (0.5f*percentCompleteCubed - 0.5f*percentCompleteSquared);
        }

        //------------------------------------------------------------------------
        public static Vector3 GetPoint(int _index, Vector3[] _path)
        {
            _index = _index > _path.Length - 1 ? _path.Length - 1 : _index;
            _index = _index < 0 ? 0 : _index;
            return _path[_index];
        }

        //------------------------------------------------------------------------
 		public static float CalculateAngle(float _x1, float _y1, float _x2, float _y2)
 		{
 			return Mathf.Atan2((_y2 - _y1), (_x2 - _x1));
 		}

        //------------------------------------------------------------------------
        public static float AngleBetween(Vector2 _vector1, Vector2 _vector2)
        {
            Vector2 diff = _vector2 - _vector1;
            float sign = (_vector2.y < _vector1.y) ? -1.0f : 1.0f;
            return Vector2.Angle(Vector2.right, diff) * sign;
        }

        //------------------------------------------------------------------------
        public static float GetAngle(Vector2 _v1, Vector2 _v2)
        {
            var sign = Mathf.Sign(_v1.x * _v2.y - _v1.y * _v2.x);
            return Vector2.Angle(_v1, _v2) * sign;
        }

        //------------------------------------------------------------------------
        public static Vector3[] CatmullRom(Vector3[] _path, int _subdivisions)
        {
            Vector3[] newPath = new Vector3[_path.Length*(int) Mathf.Pow(2, _subdivisions)];
            int c = 0;
            float step = 1.0f/Mathf.Pow(2, _subdivisions);
            for (int i = 0; i < _path.Length; i++)
            {
                for (float t = 0.0f; t < 1.0f; t += step)
                {
                    Vector3 previous = GetPoint(i - 1, _path);
                    Vector3 start = GetPoint(i, _path);
                    Vector3 end = GetPoint(i + 1, _path);
                    Vector3 next = GetPoint(i + 2, _path);
                    Vector3 point = CatmullRom(previous, start, end, next, t);
                    newPath[c] = point;
                    c++;
                }
            }
            return newPath;
        }

        //------------------------------------------------------------------------
        public static float FastSin(float _x)
        {
            float sin;
            //always wrap input angle to -PI..PI
            if (_x < -3.14159265f)
                _x += 6.28318531f;
            else if (_x > 3.14159265f)
                _x -= 6.28318531f;

            //compute sine
            if (_x < 0)
            {
                sin = 1.27323954f*_x + .405284735f*_x*_x;

                if (sin < 0)
                    sin = .225f*(sin*-sin - sin) + sin;
                else
                    sin = .225f*(sin*sin - sin) + sin;
            }
            else
            {
                sin = 1.27323954f*_x - 0.405284735f*_x*_x;

                if (sin < 0)
                    sin = .225f*(sin*-sin - sin) + sin;
                else
                    sin = .225f*(sin*sin - sin) + sin;
            }

            return sin;
        }

        //------------------------------------------------------------------------
        public static float FastCos(float _x)
        {
            float cos;
            _x += 1.57079632f;
            if (_x > 3.14159265f)
                _x -= 6.28318531f;

            if (_x < 0)
            {
                cos = 1.27323954f*_x + 0.405284735f*_x*_x;

                if (cos < 0)
                    cos = .225f*(cos*-cos - cos) + cos;
                else
                    cos = .225f*(cos*cos - cos) + cos;
            }
            else
            {
                cos = 1.27323954f*_x - 0.405284735f*_x*_x;

                if (cos < 0)
                    cos = .225f*(cos*-cos - cos) + cos;
                else
                    cos = .225f*(cos*cos - cos) + cos;
            }

            return cos;
        }


        //-----------------------------------------------------------------------------
        //http://stackoverflow.com/questions/217578/how-can-i-determine-whether-a-2d-point-is-within-a-polygon
        //http://www.ecse.rpi.edu/Homepages/wrf/Research/Short_Notes/pnpoly.html
        public static bool IsPointInPolygon(Vector2 _point, Vector2[] _polygon)
        {
            float minX = _polygon[0].x;
            float maxX = _polygon[0].x;
            float minY = _polygon[0].y;
            float maxY = _polygon[0].y;
            for(int index = 1; index < _polygon.Length; index++)
            {
                Vector2 point = _polygon[index];
                minX = Mathf.Min(point.x, minX);
                maxX = Mathf.Max(point.x, maxX);
                minY = Mathf.Min(point.y, minY);
                maxY = Mathf.Max(point.y, maxY);
            }

            if(_point.x < minX || _point.x > maxX || _point.y < minY || _point.y > maxY)
                return false;

            bool inside = false;
            for(int index = 0, j = _polygon.Length - 1; index < _polygon.Length; j = index++)
            {
                if((_polygon[index].y > _point.y) != (_polygon[j].y > _point.y) && 
                    _point.x < (_polygon[j].x - _polygon[index].x) * 
                    (_point.y - _polygon[index].y) / 
                    (_polygon[j].y - _polygon[index].y) + _polygon[index].x)
                    inside = !inside;
            }
            return inside;
        }

        //-----------------------------------------------------------------------------
        public static float SineEaseIn(float _start, float _end, float _value)
        {
            return Mathf.Lerp(_start, _end, Mathf.Sin(_value*HALF_PI - HALF_PI) + 1);
        }

        //-----------------------------------------------------------------------------
        public static float SineEaseOut(float _start, float _end, float _value)
        {
            return Mathf.Lerp(_start, _end, (Mathf.Sin(_value*Mathf.PI - HALF_PI) + 1)/2);
        }

        //-----------------------------------------------------------------------------
        public static float CubicEaseIn(float _start, float _end, float _value)
        {
            return Mathf.Lerp(_start, _end, Mathf.Pow(_value, 5));
        }

        //-----------------------------------------------------------------------------
        public static float CubicEaseOut(float _start, float _end, float _value)
        {
            return Mathf.Lerp(_start, _end, (Mathf.Pow(_value - 1, 5) + 1));
        }

        //-----------------------------------------------------------------------------
        public static float Berp(float _start, float _end, float _value)
        {
            _value = Mathf.Clamp01(_value);
            _value = (Mathf.Sin(_value*Mathf.PI*(0.2f + 2.5f*_value*_value*_value))*Mathf.Pow(1f - _value, 2.2f) +
                      _value)*(1f + (1.2f*(1f - _value)));
            return _start + (_end - _start)*_value;
        }

        //-----------------------------------------------------------------------------
        public static float SmoothStep(float _x, float _min, float _max)
        {
            _x = Mathf.Clamp(_x, _min, _max);
            float v1 = (_x - _min)/(_max - _min);
            float v2 = (_x - _min)/(_max - _min);
            return -2*v1*v1*v1 + 3*v2*v2;
        }

        //-----------------------------------------------------------------------------
        public static float Lerp(float _start, float _end, float _value)
        {
            return ((1.0f - _value)*_start) + (_value*_end);
        }

        //-----------------------------------------------------------------------------
        public static Vector3 NearestPoint(Vector3 _lineStart, Vector3 _lineEnd, Vector3 _point)
        {
            Vector3 lineDirection = Vector3.Normalize(_lineEnd - _lineStart);
            float closestPoint = Vector3.Dot((_point - _lineStart), lineDirection)/
                                 Vector3.Dot(lineDirection, lineDirection);
            return _lineStart + (closestPoint*lineDirection);
        }

        //-----------------------------------------------------------------------------
        public static Vector3 NearestPointStrict(Vector3 _lineStart, Vector3 _lineEnd, Vector3 _point)
        {
            Vector3 fullDirection = _lineEnd - _lineStart;
            Vector3 lineDirection = Vector3.Normalize(fullDirection);
            float closestPoint = Vector3.Dot((_point - _lineStart), lineDirection)/
                                 Vector3.Dot(lineDirection, lineDirection);
            return _lineStart + (Mathf.Clamp(closestPoint, 0.0f, Vector3.Magnitude(fullDirection))*lineDirection);
        }

        //-----------------------------------------------------------------------------
        public static Vector3 ClosestPointOnLine(Vector3 _linePoint1, Vector3 _linePoint2, Vector3 _point)
        {
            var vVector1 = _point - _linePoint1;
            var vVector2 = (_linePoint2 - _linePoint1).normalized;

            var d = Vector3.Distance(_linePoint1, _linePoint2);
            var t = Vector3.Dot(vVector2, vVector1);

            if(t <= 0)
                return _linePoint1;

            if(t >= d)
                return _linePoint2;

            Vector3 vVector3 = vVector2 * t;
            Vector3 vClosestPoint = _linePoint1 + vVector3;
            return vClosestPoint;
        }


        //-----------------------------------------------------------------------------
        public static float Bounce(float _x)
        {
            return Mathf.Abs(Mathf.Sin(6.28f*(_x + 1.0f)*(_x + 1.0f))*(1.0f - _x));
        }

        //-----------------------------------------------------------------------------
        // test for value that is near specified float (due to floating point inprecision) 
        public static bool Approx(float _val, float _about, float _range)
        {
            return ((Mathf.Abs(_val - _about) < _range));
        }

        //-----------------------------------------------------------------------------
        // test if a Vector3 is close to another Vector3 (due to floating point inprecision)
        // compares the square of the distance to the square of the range as this 
        // avoids calculating a square root which is much slower than squaring the range
        public static bool Approx(Vector3 _val, Vector3 _about, float _range)
        {
            return ((_val - _about).sqrMagnitude < _range*_range);
        }

        //-----------------------------------------------------------------------------
        // CLerp - Circular Lerp - is like lerp but handles the wraparound from 0 to 360.
        // This is useful when interpolating eulerAngles and the object crosses the 0/360 boundary.  The standard Lerp function causes the object
        // to rotate in the wrong direction and looks stupid. Clerp fixes that.
        public static float Clerp(float _start, float _end, float _value)
        {
            float min = 0.0f;
            float max = 360.0f;
            float half = Mathf.Abs((max - min)/2.0f); //half the distance between min and max
            float retval, diff;

            if ((_end - _start) < -half)
            {
                diff = ((max - _start) + _end)*_value;
                retval = _start + diff;
            }
            else if ((_end - _start) > half)
            {
                diff = -((max - _end) + _start)*_value;
                retval = _start + diff;
            }
            else retval = _start + (_end - _start)*_value;
            // Debug.Log("Start: "  + start + "   End: " + end + "  Value: " + value + "  Half: " + half + "  Diff: " + diff + "  Retval: " + retval);
            return retval;
        }

        //-----------------------------------------------------------------------------
        public static int GetSmallestRotationDirection(float _objectRotationRad, float _radBetween,
            float _errorRad = 0.0f)
        {
            _objectRotationRad = SimplifyRad(_objectRotationRad);
            _radBetween = SimplifyRad(_radBetween);

            _radBetween += -_objectRotationRad;
            _radBetween = SimplifyRad(_radBetween);
            if (_radBetween < -_errorRad)
                return -1;
            return _radBetween > _errorRad ? 1 : 0;
        }

        //-----------------------------------------------------------------------------
        public static float SimplifyRad(float _rad)
        {
            if (!(_rad > Mathf.PI) && _rad >= -Mathf.PI)
                return _rad;

            float newRad = _rad - (int) (_rad/(Mathf.PI*2))*(Mathf.PI*2);
            if (_rad > 0)
            {
                if (newRad < Mathf.PI)
                    return newRad;

                newRad = -(Mathf.PI*2 - newRad);
                return newRad;
            }

            if (newRad > -Mathf.PI)
                return newRad;

            return (Mathf.PI*2) + newRad;
        }

        //-----------------------------------------------------------------------------
        public static Vector3 CalculateBallisticForce(Vector3 _origin, Vector3 _target, float _timeToTarget)
        {
            // calculate vectors
            Vector3 toTarget = _target - _origin;
            Vector3 toTargetXZ = toTarget;
            toTargetXZ.y = 0;

            // calculate xz and y
            float y = toTarget.y;
            float xz = toTargetXZ.magnitude;

            // calculate starting speeds for xz and y. Physics forumulase deltaX = v0 * t + 1/2 * a * t * t
            // where a is "-gravity" but only on the y plane, and a is 0 in xz plane.
            // so xz = v0xz * t => v0xz = xz / t
            // and y = v0y * t - 1/2 * gravity * t * t => v0y * t = y + 1/2 * gravity * t * t => v0y = y / t + 1/2 * gravity * t
            float t = _timeToTarget;
            float v0Y = y / t + 0.5f * Physics.gravity.magnitude * t;
            float v0XZ = xz / t;

            // create result vector for calculated starting speeds
            Vector3 result = toTargetXZ.normalized;        // get direction of xz but with magnitude 1
            result *= v0XZ;                                // set magnitude of xz to v0xz (starting speed in xz plane)
            result.y = v0Y;                                // set y to v0y (starting speed of y plane)

            return result;
        }

        //-----------------------------------------------------------------------------
        public static Vector3 CalculateBallisticVelocity2(Vector3 _origin, Vector3 _target, float _angle)
        {
            float gravity = Physics.gravity.magnitude;
            // Selected angle in radians
            float angle = _angle * Mathf.Deg2Rad;

            // Positions of this object and the target on the same plane
            m_planarTarget.Set(_target.x, 0, _target.z);
            m_planarPos.Set(_origin.x, 0, _origin.z);

            // Planar distance between objects
            float distance = Vector3.Distance(m_planarTarget, m_planarPos);
            // Distance along the y axis between objects
            float yOffset = _origin.y - _target.y;

            float initialVelocity = (1 / Mathf.Cos(angle)) * Mathf.Sqrt(0.5f * gravity * Mathf.Pow(distance, 2) / (distance * Mathf.Tan(angle) + yOffset));
            if (float.IsNaN(initialVelocity))
                m_velocity.Set(0, Mathf.Sin(angle), Mathf.Cos(angle));
            else
                m_velocity.Set(0, initialVelocity * Mathf.Sin(angle), initialVelocity * Mathf.Cos(angle));

            // Rotate our velocity to match the direction between the two objects
            //float angleBetweenObjects = Vector3.Angle(Vector3.forward, planarTarget - planarPosition);
            float angleBetweenObjects = Vector3.Angle(Vector3.forward, m_planarTarget - m_planarPos) * (_target.x > _origin.x ? 1 : -1);
            return Quaternion.AngleAxis(angleBetweenObjects, Vector3.up) * m_velocity;
        }

        //-----------------------------------------------------------------------------
        public static Vector3 CalculateBallisticVelocity(Vector3 _origin, Vector3 _target, float _angle)
        {
            if(System.Math.Abs(_angle - 90) < TOLERANCE || System.Math.Abs(_angle + 90) < TOLERANCE)
                return Vector3.zero;

            float radAngle = Mathf.Deg2Rad*_angle;
            Vector3 atoB = _target - _origin;
            Vector3 horizontal = GetHorizontalVector(atoB, Physics.gravity);
            float horizontalDistance = horizontal.magnitude;
            Vector3 vertical = GetVerticalVector(atoB, Physics.gravity);
            float verticalDistance = vertical.magnitude * Mathf.Sign(Vector3.Dot(vertical, -Physics.gravity));

            float angleX = Mathf.Cos(radAngle);
            float angleY = Mathf.Sin(radAngle);
            if(verticalDistance / horizontalDistance > angleY / angleX)
                return Vector3.zero;

            float gravityMag = Physics.gravity.magnitude;
            float destSpeed = (1 / angleX) * Mathf.Sqrt((0.5f * gravityMag * horizontalDistance * horizontalDistance) / ((horizontalDistance * Mathf.Tan(radAngle)) - verticalDistance));
            Vector3 launch = ((horizontal.normalized * angleX) - (Physics.gravity.normalized * angleY)) * destSpeed;
            return launch;
        }

        //-----------------------------------------------------------------------------
        public static Vector3 CalculateBallisticVelocityOriginal(Vector3 _origin, Vector3 _target, float _angle)
        {
            float gravity = Physics.gravity.magnitude;
            // Selected angle in radians
            float angle = _angle * Mathf.Deg2Rad;

            // Positions of this object and the target on the same plane
            Vector3 planarTarget = new Vector3(_target.x, 0, _target.z);
            Vector3 planarPosition = new Vector3(_origin.x, 0, _origin.z);

            // Planar distance between objects
            float distance = Vector3.Distance(planarTarget, planarPosition);
            // Distance along the y axis between objects
            float yOffset = _origin.y - _target.y;

            float initialVelocity = (1 / Mathf.Cos(angle)) * Mathf.Sqrt((0.5f * gravity * Mathf.Pow(distance, 2)) / (distance * Mathf.Tan(angle) + yOffset));

            Vector3 velocity = new Vector3(0, initialVelocity * Mathf.Sin(angle), initialVelocity * Mathf.Cos(angle));

            // Rotate our velocity to match the direction between the two objects
            //float angleBetweenObjects = Vector3.Angle(Vector3.forward, planarTarget - planarPosition);
            float angleBetweenObjects = Vector3.Angle(Vector3.forward, planarTarget - planarPosition) * (_target.x > _origin.x ? 1 : -1);
            return Quaternion.AngleAxis(angleBetweenObjects, Vector3.up) * velocity;
        }


        //-----------------------------------------------------------------------------
        public static Vector3 GetHorizontalVector(Vector3 _atoB, Vector3 _gravityBase)
        {
            Vector3 perpendicular = Vector3.Cross(_atoB, _gravityBase);
            perpendicular = Vector3.Cross(_gravityBase, perpendicular);
            return Vector3.Project(_atoB, perpendicular);
        }

        //-----------------------------------------------------------------------------
        public static Vector3 GetVerticalVector(Vector3 _atoB, Vector3 _gravityBase)
        {
            return Vector3.Project(_atoB, _gravityBase);
        }

        //-----------------------------------------------------------------------------
        public static float CalculateBallisticDistance(float _velocity, float _angle)
        {
            return (Mathf.Pow(_velocity, 2) * Mathf.Sin(2 * (_angle * Mathf.Deg2Rad))) / Physics.gravity.magnitude;
        }

        //------------------------------------------------------------------
        public static void CalculateTrajectoryPoints(Transform _transform, float _maxTime, float _force, int _maxPoints, ref List<Vector3> _trajectoryPoints)
        {
            Vector3 pos = _transform.position;
            Vector3 velocityVector = CalculateForce(_transform, _force);
            _trajectoryPoints.Clear();
            float fTime = 0.0f;
            for (int index = 0; index < _maxPoints; index++)
            {
                float x = velocityVector.x * fTime + pos.x;
                float y = (velocityVector.y * fTime) + (0.5f * Physics.gravity.y * fTime * fTime) + pos.y;
                Vector3 trajectoryPos = new Vector3(x, y, 0);
                _trajectoryPoints.Add(trajectoryPos);
                fTime += _maxTime / _maxPoints;
            }
        }

        //------------------------------------------------------------------
        public static Vector2 GetPointOnLineIntersection(Vector2 _lineP1, Vector2 _lineP2, Vector2 _point)
        {
            float x1 = _lineP1.x, y1 = _lineP1.y, x2 = _lineP2.x, y2 = _lineP2.y, x3 = _point.x, y3 = _point.y;
            float px = x2 - x1, py = y2 - y1;
            float dAb = px * px + py * py;
            float u = ((x3 - x1) * px + (y3 - y1) * py) / dAb;
            float x = x1 + u * px, y = y1 + u * py;
            return new Vector2(x, y);
        }

        //------------------------------------------------------------------
        public static Vector3 CalculateForce(Transform _transform, float _force)
        {
            float angle = _transform.eulerAngles.z * Mathf.Deg2Rad;
            float sin = Mathf.Sin(angle);
            float cos = Mathf.Cos(angle);
            Vector2 direction = Vector2.up;
            Vector2 forward = new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
            return (forward * _force) / 12.5f; //TODO find out why we have to do this
        }

        //------------------------------------------------------------------
        public static string FormatBigNumbers(double _bigNumber, int _decimalNumbers = 1, bool _longsuffixes = false)
        {
            if (_decimalNumbers == 1)
                _decimalNumbers = System.Math.Abs(_bigNumber) < 1000? 0 : 2;

            int index = 0;
            while(System.Math.Abs(_bigNumber) >= 1000)
            {
                _bigNumber /= 1000;
                index++;
            }
            if (_decimalNumbers > 0)
            {
                double factor = 1;
                for (int decimalIndex = 0; decimalIndex < _decimalNumbers; decimalIndex++)
                    factor *= 10;
                _bigNumber = System.Math.Floor(_bigNumber*factor)/factor;
            }
            else
                _bigNumber = System.Math.Round(_bigNumber);

            if(_longsuffixes)
                return _bigNumber.ToString() + " " + LONG_SUFFIXES[index< LONG_SUFFIXES.Length?index: LONG_SUFFIXES.Length-1];
            return _bigNumber.ToString() + " "+ SUFFIXES[index < SUFFIXES.Length ? index : SUFFIXES.Length - 1];
        }

    }
}