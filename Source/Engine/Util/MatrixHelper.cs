using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace System.Numerics;
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
}