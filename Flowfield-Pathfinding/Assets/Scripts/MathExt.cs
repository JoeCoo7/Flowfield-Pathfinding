using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace RSGLib.ECS
{
    public static class MathExt
    {
        public static float2 Float2Zero() { return new float2(0f, 0f);}
        public static float2 Float2One() { return new float2(1f, 1f); }
        public static float2 Float2Up() { return new float2(0f, 1f); }
        public static float2 Float2Down() { return new float2(0f, -1f); }
        public static float2 Float2Left() { return new float2(-1f, 0f); }
        public static float2 Float2Right() { return new float2(1f, 0f); }
        public static float3 Float3Zero() { return new float3(0f, 0f, 0f); }
        public static float3 Float3One() { return new float3(1f, 1f, 1f); }
        public static float3 Float3Up() { return new float3(0f, 1f, 0f); }
        public static float3 Float3Down() { return new float3(0f, -1f, 0f); }
        public static float3 Float3Left() { return new float3(-1f, 0f, 0f); }
        public static float3 Float3Right() { return new float3(1f, 0f, 0f); }
        public static float3 Float3Forward() { return new float3(0f, 0f, 1f); }
        public static float3 Float3Back() { return new float3(0f, 0f, -1f); }

        //-----------------------------------------------------------------------------
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float SignedAngle(float2 _from, float2 _to)
        {
            float2 from = math.normalize(_from);
            float2 to = math.normalize(_to);
            float num = math.acos(math.clamp(math.dot(from, to), -1f, 1f)) * 57.29578f;
            float num2 = math.sign(from.x * to.y - from.y * to.x);
            return num * num2;
        }

        //-----------------------------------------------------------------------------
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float2 PointOnCircle(float _radius, float _degAngle, float2 _origin)
        {
            float radAngle = math.radians(_degAngle);
            return new float2(_radius * math.cos(radAngle) + _origin.x, _radius * math.sin(radAngle) + _origin.y);
        }

        //-----------------------------------------------------------------------------
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static bool CirclesCollide(float2 _origin1, float _radius1, float2 _origin2, float _radius2)
        {
            float radius = _radius1 + _radius2;
            float deltaX = _origin1.x - _origin2.x;
            float deltaY = _origin1.y - _origin2.y; ;
            return deltaX * deltaX + deltaY * deltaY <= radius * radius;
        }

        //-----------------------------------------------------------------------------
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static bool CirclesCollide(float3 _origin1, float _radius1, float3 _origin2, float _radius2)
        {
            float radius = _radius1 + _radius2;
            float deltaX = _origin1.x - _origin2.x;
            float deltaY = _origin1.z - _origin2.z; 
            return deltaX * deltaX + deltaY * deltaY <= radius * radius;
        }
        
        //-----------------------------------------------------------------------------
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static bool SphereCollision(float3 _origin1, float _radius1, float3 _origin2, float _radius2)
        {
            float3 difference = _origin2 - _origin1;
            float distanceSqr = math.dot(difference, difference);
            float radiiSqr = _radius1 + _radius2;
            radiiSqr *= radiiSqr;
            return distanceSqr <= radiiSqr;
        }

        //-----------------------------------------------------------------------------
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float4x4 Invert(float4x4 _value)
        {
            var result = new float4x4();
            float b0 = (_value.c2.x * _value.c3.y) - (_value.c2.y * _value.c3.x);
            float b1 = (_value.c2.x * _value.c3.z) - (_value.c2.z * _value.c3.x);
            float b2 = (_value.c2.w * _value.c3.x) - (_value.c2.x * _value.c3.w);
            float b3 = (_value.c2.y * _value.c3.z) - (_value.c2.z * _value.c3.y);
            float b4 = (_value.c2.w * _value.c3.y) - (_value.c2.y * _value.c3.w);
            float b5 = (_value.c2.z * _value.c3.w) - (_value.c2.w * _value.c3.z);
            float d11 = _value.c1.y * b5 + _value.c1.z * b4 + _value.c1.w * b3;
            float d12 = _value.c1.x * b5 + _value.c1.z * b2 + _value.c1.w * b1;
            float d13 = _value.c1.x * -b4 +_value.c1.y * b2 + _value.c1.w * b0;
            float d14 = _value.c1.x * b3 + _value.c1.y * -b1 +_value.c1.z * b0;

            float det = _value.c0.x * d11 - _value.c0.y * d12 + _value.c0.z * d13 - _value.c0.w * d14;
            if (Math.Abs(math.abs(det)) < math.epsilon_normal)
                return float4x4.identity;

            det = 1f / det;

            float a0 = (_value.c0.x * _value.c1.y) - (_value.c0.y * _value.c1.x);
            float a1 = (_value.c0.x * _value.c1.z) - (_value.c0.z * _value.c1.x);
            float a2 = (_value.c0.w * _value.c1.x) - (_value.c0.x * _value.c1.w);
            float a3 = (_value.c0.y * _value.c1.z) - (_value.c0.z * _value.c1.y);
            float a4 = (_value.c0.w * _value.c1.y) - (_value.c0.y * _value.c1.w);
            float a5 = (_value.c0.z * _value.c1.w) - (_value.c0.w * _value.c1.z);

            float d21 = _value.c0.y * b5 + _value.c0.z * b4 + _value.c0.w * b3;
            float d22 = _value.c0.x * b5 + _value.c0.z * b2 + _value.c0.w * b1;
            float d23 = _value.c0.x * -b4 +_value.c0.y * b2 + _value.c0.w * b0;
            float d24 = _value.c0.x * b3 + _value.c0.y * -b1 +_value.c0.z * b0;
            float d31 = _value.c3.y * a5 + _value.c3.z * a4 + _value.c3.w * a3;
            float d32 = _value.c3.x * a5 + _value.c3.z * a2 + _value.c3.w * a1;
            float d33 = _value.c3.x * -a4 +_value.c3.y * a2 + _value.c3.w * a0;
            float d34 = _value.c3.x * a3 + _value.c3.y * -a1 +_value.c3.z * a0;
            float d41 = _value.c2.y * a5 + _value.c2.z * a4 + _value.c2.w * a3;
            float d42 = _value.c2.x * a5 + _value.c2.z * a2 + _value.c2.w * a1;
            float d43 = _value.c2.x * -a4 +_value.c2.y * a2 + _value.c2.w * a0;
            float d44 = _value.c2.x * a3 + _value.c2.y * -a1 +_value.c2.z * a0;

            result.c0.x = +d11 * det; result.c0.y = -d21 * det; result.c0.z = +d31 * det; result.c0.w = -d41 * det;
            result.c1.x = -d12 * det; result.c1.y = +d22 * det; result.c1.z = -d32 * det; result.c1.w = +d42 * det;
            result.c2.x = +d13 * det; result.c2.y = -d23 * det; result.c2.z = +d33 * det; result.c2.w = -d43 * det;
            result.c3.x = -d14 * det; result.c3.y = +d24 * det; result.c3.z = -d34 * det; result.c3.w = +d44 * det;
            return result;
        }   
        
        //-----------------------------------------------------------------------------
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float3 Mul(float4x4 _matrix, float3 _point)
        {
            return math.mul(_matrix, new float4(_point, 1)).xyz;
        }
        
        //https://en.wikipedia.org/wiki/Conversion_between_quaternions_and_Euler_angles
        [MethodImpl((MethodImplOptions) 0x100)] // agressive inline
        public static quaternion Euler(float _roll, float _pitch, float _yaw)
        {
            float cy = math.cos(_yaw * 0.5f);
            float sy = math.sin(_yaw * 0.5f);
            float cr = math.cos(_roll * 0.5f);
            float sr = math.sin(_roll * 0.5f);
            float cp = math.cos(_pitch * 0.5f);
            float sp = math.sin(_pitch * 0.5f);
            return new quaternion(
                cy * sr * cp - sy * cr * sp,
                cy * cr * sp + sy * sr * cp,
                sy * cr * cp - cy * sr * sp,
                cy * cr * cp + sy * sr * sp);
        }

        //-----------------------------------------------------------------------------
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static quaternion Euler(float3 _eulers)
        {
            return Euler(_eulers.x, _eulers.y, _eulers.z);
        }
    }
}


