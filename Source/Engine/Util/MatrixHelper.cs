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

    public static Matrix4x4 Inverse(this Matrix4x4 m)
    {
        Matrix4x4.Invert(m, out var r);
        return r;
    }
    public static Vector3 ToEuler(this Quaternion quaternion)
    {
        float yaw, pitch, roll;

        // 计算欧拉角
        double sqw = quaternion.W * quaternion.W;
        double sqx = quaternion.X * quaternion.X;
        double sqy = quaternion.Y * quaternion.Y;
        double sqz = quaternion.Z * quaternion.Z;
        double unit = sqx + sqy + sqz + sqw; // 单位化因子

        double test = quaternion.X * quaternion.Y + quaternion.Z * quaternion.W;
        if (test > 0.499 * unit) // 包含极限情况的优化
        {
            yaw = (float)(2 * Math.Atan2(quaternion.X, quaternion.W));
            pitch = (float)(Math.PI / 2);
            roll = 0;
        }
        else if (test < -0.499 * unit)
        {
            yaw = (float)(-2 * Math.Atan2(quaternion.X, quaternion.W));
            pitch = (float)(-Math.PI / 2);
            roll = 0;
        }
        else
        {
            yaw = (float)Math.Atan2(2 * quaternion.Y * quaternion.W - 2 * quaternion.X * quaternion.Z, sqx - sqy - sqz + sqw);
            pitch = (float)Math.Asin(2 * test / unit);
            roll = (float)Math.Atan2(2 * quaternion.X * quaternion.W - 2 * quaternion.Y * quaternion.Z, -sqx + sqy - sqz + sqw);
        }

        // 返回欧拉角
        return new Vector3(pitch, yaw, roll);
    }

}

public static class PlaneHelper
{
    public static float Point2Plane(this Plane plane, Vector3 Point)
    {
        return plane.Normal.X * Point.X + plane.Normal.Y * Point.Y + plane.Normal.Z * Point.Z + plane.D;
    }
}