﻿
using Jitter2.Dynamics;
using SharpGLTF.Schema2;
using Silk.NET.Maths;
using Spark.Engine.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Spark.Engine.Assets;

public interface ISerializable
{
    public void Serialize(BinaryWriter Writer, Engine engine);

    public void Deserialize(BinaryReader Reader, Engine engine);


    public static void AssetSerialize<T>(T? asset, BinaryWriter Writer, Engine engine) where T : AssetBase
    {
        var bw = new BinaryWriter(Writer.BaseStream);
        if (asset == null || string.IsNullOrEmpty(asset.Path))
        {
            bw.WriteInt32(0);
        }
        else
        {
            var str = Encoding.UTF8.GetBytes(asset.Path);
            bw.WriteInt32(str.Length);
            bw.Write(str);
        }
    }
    public static T? AssetDeserialize<T>(BinaryReader br, Engine engine) where T : AssetBase, new()
    {
        var len = br.ReadInt32();
        if (len == 0)
            return null;
        var str = br.ReadBytes(len);
        var Path = Encoding.UTF8.GetString(str);
        return engine.AssetMgr.Load<T>(Path);
    }

    public static AssetBase? AssetDeserialize(Type type,BinaryReader br, Engine engine)
    {
        var len = br.ReadInt32();
        if (len == 0)
            return null;
        var str = br.ReadBytes(len);
        var Path = Encoding.UTF8.GetString(str);
        return engine.AssetMgr.Load(type, Path);
    }


    public static void ReflectionDeserialize(object obj, BinaryReader br, Engine engine)
    {
        var type = obj.GetType();
        var properties = new List<PropertyInfo>();
        foreach (var property in type.GetProperties())
        {
            if (property.CanWrite && property.CanRead == false)
                continue;
            var att = property.GetAttribute<PropertyAttribute>();
            if (att == null)
                continue;
            if (att.IgnoreSerialize)
                continue;
            ReflectionDeSerialize(property, obj, br, engine);
        }
    }
    public static void ReflectionSerialize(object obj, BinaryWriter bw, Engine engine)
    {
        var type = obj.GetType();

        var properties = new List<PropertyInfo>();
        foreach(var property in type.GetProperties())
        {
            if (property.CanWrite && property.CanRead == false)
                continue;
            var att = property.GetAttribute<PropertyAttribute>();
            if (att == null)
                continue;
            if (att.IgnoreSerialize) 
                continue;
            properties.Add(property);
        }
        foreach(var property in properties)
        {
            ReflectionSerialize(property, obj, bw, engine);
        }
    }

    public static void ReflectionDeSerialize(PropertyInfo property, object obj, BinaryReader br, Engine engine)
    {
        if (property.PropertyType == typeof(int))
        {
            property.SetValue(obj, br.ReadInt32());
        }
        else if (property.PropertyType == typeof(uint))
        {
            property.SetValue(obj, br.ReadUInt32());
        }
        else if (property.PropertyType == typeof(long))
        {
            property.SetValue(obj, br.ReadInt64());
        }
        else if (property.PropertyType == typeof(ulong))
        {
            property.SetValue(obj, br.ReadUInt64());
        }
        else if (property.PropertyType == typeof(float))
        {
            property.SetValue(obj, br.ReadSingle());
        }
        else if (property.PropertyType == typeof(double))
        {
            property.SetValue(obj, br.ReadDouble());
        }
        else if (property.PropertyType == typeof(string))
        {
            property.SetValue(obj, br.ReadString2());
        }
        else if (property.PropertyType.IsSubclassOf(typeof(AssetBase)))
        {
            property.SetValue(obj, AssetDeserialize(property.PropertyType, br, engine));
        }
        else if (property.PropertyType == typeof(Matrix4x4))
        {
            property.SetValue(obj, br.ReadMatrix4x4());
        }
        else if (property.PropertyType == typeof(Vector3))
        {
            property.SetValue(obj, br.ReadVector3());
        }
        else if (property.PropertyType == typeof(Vector2))
        {
            property.SetValue(obj, br.ReadVector2());
        }
        else if (property.PropertyType == typeof(Vector4))
        {
            property.SetValue(obj, br.ReadVector4());
        }
    }
    public static void ReflectionSerialize(PropertyInfo property, object obj , BinaryWriter bw, Engine engine)
    {
        var value = property.GetValue(obj);
        if (value is int v1)
        {
            bw.WriteInt32(v1);
        }
        else if (value is uint v2)
        {
            bw.WriteUInt32(v2);
        }
        else if (value is long v3)
        {
            bw.WriteInt64(v3);
        }
        else if (value is ulong v4)
        {
            bw.WriteUInt64(v4);
        }
        else if (value is float v5)
        {
            bw.WriteSingle(v5);
        }
        else if (value is double v6)
        {
            bw.WriteDouble(v6);
        }
        else if (value is string v7)
        {
            bw.WriteString2(v7);
        }
        else if (value is AssetBase v8)
        {
            AssetSerialize(v8, bw, engine);
        }
        else if (value is Matrix4x4 v9)
        {
            bw.Write(v9);
        }
        else if (value is Vector3 v10)
        {
            bw.Write(v10);
        }
        else if (value is Vector2 v11)
        {
            bw.Write(v11);
        }
        else if (value is Vector4 v12)
        {
            bw.Write(v12);
        }
    }

}


public static class StreamHelper
{
    public static void WriteInt32(this BinaryWriter bw, int v)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BitConverter.TryWriteBytes(bytes, v);
        bw.Write(bytes);
    }

    public static void WriteUInt32(this BinaryWriter bw, uint v)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BitConverter.TryWriteBytes(bytes, v);
        bw.Write(bytes);
    }


    public static void WriteInt64(this BinaryWriter bw, long v)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BitConverter.TryWriteBytes(bytes, v);
        bw.Write(bytes);
    }

    public static void WriteUInt64(this BinaryWriter bw, ulong v)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        BitConverter.TryWriteBytes(bytes, v);
        bw.Write(bytes);
    }


    public static void WriteSingle(this BinaryWriter bw, float v)
    {
        Span<byte> bytes = stackalloc byte[sizeof(float)];
        BitConverter.TryWriteBytes(bytes, v);
        bw.Write(bytes);
    }

    public static void WriteDouble(this BinaryWriter bw, double v)
    {
        Span<byte> bytes = stackalloc byte[sizeof(double)];
        BitConverter.TryWriteBytes(bytes, v);
        bw.Write(bytes);
    }


    public static void WriteInt16(this BinaryWriter bw, short v)
    {
        Span<byte> bytes = stackalloc byte[sizeof(short)];
        BitConverter.TryWriteBytes(bytes, v);
        bw.Write(bytes);
    }

    public static void WriteUInt16(this BinaryWriter bw, ushort v)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        BitConverter.TryWriteBytes(bytes, v);
        bw.Write(bytes);
    }


    public static void WriteString2(this BinaryWriter bw, string? str)
    {
        if (string.IsNullOrEmpty(str))
        {
            bw.WriteInt32(0);
        }
        else
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            bw.WriteInt32(bytes.Length);
            bw.Write(bytes);
        }
    }
    public static string ReadString2(this BinaryReader br)
    {
        var len = br.ReadInt32();
        if (len == 0)
            return string.Empty;
        var str = br.ReadBytes(len);
        return Encoding.UTF8.GetString(str);
    }

    public static void Write(this BinaryWriter bw, Vector3 v)
    {
        bw.WriteSingle(v.X);
        bw.WriteSingle(v.Y);
        bw.WriteSingle(v.Z);
    }

    public static void Write(this BinaryWriter bw, Vector2 v)
    {
        bw.WriteSingle(v.X);
        bw.WriteSingle(v.Y);
    }
    public static void Write(this BinaryWriter bw, Vector4 v)
    {
        bw.WriteSingle(v.X);
        bw.WriteSingle(v.Y);
        bw.WriteSingle(v.Z);
        bw.WriteSingle(v.W);
    }
    public static void Write(this BinaryWriter bw, Quaternion q)
    {
        bw.WriteSingle(q.X);
        bw.WriteSingle(q.Y);
        bw.WriteSingle(q.Z);
        bw.WriteSingle(q.W);
    }
    public static void Write(this BinaryWriter bw, in Matrix4x4 m)
    {
        bw.WriteSingle(m.M11);
        bw.WriteSingle(m.M12);
        bw.WriteSingle(m.M13);
        bw.WriteSingle(m.M14);

        bw.WriteSingle(m.M21);
        bw.WriteSingle(m.M22);
        bw.WriteSingle(m.M23);
        bw.WriteSingle(m.M24);

        bw.WriteSingle(m.M31);
        bw.WriteSingle(m.M32);
        bw.WriteSingle(m.M33);
        bw.WriteSingle(m.M34);

        bw.WriteSingle(m.M41);
        bw.WriteSingle(m.M42);
        bw.WriteSingle(m.M43);
        bw.WriteSingle(m.M44);
    }


    public static Vector3 ReadVector3(this BinaryReader br)
    {
        var v = new Vector3();
        v.X = br.ReadSingle();
        v.Y = br.ReadSingle();
        v.Z = br.ReadSingle();
        return v;
    }

    public static Vector2 ReadVector2(this BinaryReader br)
    {
        var v = new Vector2();
        v.X = br.ReadSingle();
        v.Y = br.ReadSingle();
        return v;
    }
    public static Vector4 ReadVector4(this BinaryReader br)
    {
        var v = new Vector4();
        v.X = br.ReadSingle();
        v.Y = br.ReadSingle();
        v.Z = br.ReadSingle();
        v.W = br.ReadSingle();
        return v;
    }

    public static Quaternion ReadQuaternion(this BinaryReader br)
    {
        var q = new Quaternion();
        q.X = br.ReadSingle();
        q.Y = br.ReadSingle();
        q.Z = br.ReadSingle();
        q.W = br.ReadSingle();
        return q;
    }
    public static Matrix4x4 ReadMatrix4x4(this BinaryReader br)
    {
        var m = new Matrix4x4();
        m.M11 = br.ReadSingle();
        m.M12 = br.ReadSingle();
        m.M13 = br.ReadSingle();
        m.M14 = br.ReadSingle();

        m.M21 = br.ReadSingle();
        m.M22 = br.ReadSingle();
        m.M23 = br.ReadSingle();
        m.M24 = br.ReadSingle();

        m.M31 = br.ReadSingle();
        m.M32 = br.ReadSingle();
        m.M33 = br.ReadSingle();
        m.M34 = br.ReadSingle();

        m.M41 = br.ReadSingle();
        m.M42 = br.ReadSingle();
        m.M43 = br.ReadSingle();
        m.M44 = br.ReadSingle();

        return m;

    }

}

public static class MagicCode
{
    public static int Asset = 19980625;
    public static int Texture = 1;
    public static int TextureCube = 2;
    public static int TextureHDR = 2;
    public static int StaticMesh = 3;
    public static int SkeletalMesh = 4;
    public static int Material = 5;
    public static int Skeleton = 6;
    public static int AnimSequence = 7;
    public static int Actor = 8;

}