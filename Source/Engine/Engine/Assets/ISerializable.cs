
using Jitter2.Dynamics;
using SharpGLTF.Schema2;
using Silk.NET.Maths;
using Spark.Engine.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Spark.Engine.Assets;

public interface ISerializable
{
    public void Serialize(StreamWriter Writer, Engine engine);

    public void Deserialize(StreamReader Reader, Engine engine);


    public static void AssetSerialize<T>(T? asset, StreamWriter Writer, Engine engine) where T : AssetBase
    {
        var bw = new BinaryWriter(Writer.BaseStream);
        if (asset == null || string.IsNullOrEmpty(asset.Path))
        {
            bw.Write(BitConverter.GetBytes(0));
        }
        else
        {
            var str = Encoding.UTF8.GetBytes(asset.Path);
            bw.Write(BitConverter.GetBytes(str.Length));
            bw.Write(str);
        }
    }
    public static T? AssetDeserialize<T>(StreamReader Reader, Engine engine) where T : AssetBase, new()
    {
        var br = new BinaryReader(Reader.BaseStream);
        var len = br.ReadInt32();
        if (len == 0)
            return null;
        var str = br.ReadBytes(len);
        var Path = Encoding.UTF8.GetString(str);
        return engine.AssetMgr.Load<T>(Path);
    }


    public static void ReflectionSerialize(object obj, StreamWriter Writer, Engine engine)
    {
        var type = obj.GetType();

        foreach(var property in type.GetProperties())
        {
            if (property.CanWrite && property.CanRead == false)
                continue;
            var att = property.GetAttribute<PropertyAttribute>();
            if (att == null)
                continue;
            if (att.IgnoreSerialize) 
                continue;
            ReflectionSerialize(property, obj, Writer, engine);
        }
    }
    public static void ReflectionSerialize(PropertyInfo property, object obj , StreamWriter Writer, Engine engine)
    {
        var bw = new BinaryWriter(Writer.BaseStream);
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
            AssetSerialize(v8, Writer, engine);
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


    public static void WriteString2(this BinaryWriter bw, string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            bw.Write(BitConverter.GetBytes(0));
        }
        else
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            bw.Write(BitConverter.GetBytes(bytes.Length));
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