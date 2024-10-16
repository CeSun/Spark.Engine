﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace Spark.Util;
public static class MatrixHelper
{
    // 计算缩放分量
    public static Vector3 Scale(this Matrix4x4 matrix)
    {
        var Vector1 = new Vector3()
        {
            X = matrix.M11,
            Y = matrix.M21,
            Z = matrix.M31,
        };
        var Vector2 = new Vector3()
        {
            X = matrix.M12,
            Y = matrix.M22,
            Z = matrix.M32,
        }; 
        var Vector3 = new Vector3()
        {
            X = matrix.M13,
            Y = matrix.M23,
            Z = matrix.M33,
        };
        return new Vector3
        {
            X = Vector1.Length() / 1.0f,
            Y = Vector2.Length() / 1.0f,
            Z = Vector3.Length() / 1.0f
        };
    }
    public static Quaternion Rotation(this Matrix4x4 matrix)
    {
        var vector1 = new Vector3()
        {
            X = matrix.M11,
            Y = matrix.M21,
            Z = matrix.M31,
        };
        vector1 = Vector3.Normalize(vector1);
        var vector2 = new Vector3()
        {
            X = matrix.M12,
            Y = matrix.M22,
            Z = matrix.M32,
        };
        vector2 = Vector3.Normalize(vector2);
        var vector3 = new Vector3()
        {
            X = matrix.M13,
            Y = matrix.M23,
            Z = matrix.M33,
        };
        vector3 = Vector3.Normalize(vector3);

        var RotationMatrix = new Matrix4x4
        {
            M11 = vector1.X,
            M21 = vector1.Y,
            M31 = vector1.Z,
            M12 = vector2.X,
            M22 = vector2.Y,
            M32 = vector2.Z,
            M13 = vector3.X,
            M23 = vector3.Y,
            M33 = vector3.Z,
        };

        return Quaternion.CreateFromRotationMatrix(RotationMatrix);
    }

    public static Matrix4x4 CreateTransform(Vector3 Location, Quaternion Rotation, Vector3 Scale)
    {

        var LocationMatrix = Matrix4x4.CreateTranslation(Location);
        var RotationMatrix = Matrix4x4.CreateFromQuaternion(Rotation);
        var ScaleMatrix = Matrix4x4.CreateScale(Scale);
        return ScaleMatrix * RotationMatrix * LocationMatrix;
    }

    public static Matrix4x4 Inverse(this Matrix4x4 m)
    {
        Matrix4x4.Invert(m, out var r);
        return r;
    }
    public static Vector3 ToEuler(this Quaternion quaternion)
    {
        Vector3 angles = new();

        float sinr_cosp = 2 * (quaternion.W * quaternion.Z + quaternion.X * quaternion.Y);
        float cosr_cosp = 1 - 2 * (quaternion.Z * quaternion.Z + quaternion.X * quaternion.X);
        angles.Z = MathF.Atan2(sinr_cosp, cosr_cosp);

        float sinp = 2 * (quaternion.W * quaternion.X - quaternion.Y * quaternion.Z);
        if (Math.Abs(sinp) >= 1)
        {
            angles.X = MathF.CopySign(MathF.PI / 2, sinp);
        }
        else
        {
            angles.X = MathF.Asin(sinp);
        }

        float siny_cosp = 2 * (quaternion.W * quaternion.Y + quaternion.Z * quaternion.X);
        float cosy_cosp = 1 - 2 * (quaternion.X * quaternion.X + quaternion.Y * quaternion.Y);
        angles.Y = MathF.Atan2(siny_cosp, cosy_cosp);

        return angles;
    }

    public static Matrix4x4 AsMatrix3x3(this Matrix4x4 m)
    {
       return new Matrix4x4()
        {
            M11 = m.M11,
            M12 = m.M12,
            M13 = m.M13,
            M14 = 0,
            M21 = m.M21,
            M22 = m.M22,
            M23 = m.M23,
            M24 = 0,
            M31 = m.M31,
            M32 = m.M32,
            M33 = m.M33,
            M34 = 0,
            M41 = 0,
            M42 = 0,
            M43 = 0,
            M44 = 0
        };

    }

    public static Vector3 VectorToPoint(this Vector4 v)
    {
        var v2 = v / v.W;
        return v2.AsVector3();
    }
}

public static class PlaneHelper
{
    public static float Point2Plane(this Plane plane, Vector3 Point)
    {
        return plane.Normal.X * Point.X + plane.Normal.Y * Point.Y + plane.Normal.Z * Point.Z + plane.D;
    }
}