
using SharpGLTF.Schema2;
using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Assets;

public interface ISerializable
{
    public void Serialize(StreamWriter Writer, Engine engine);

    public void Deserialize(StreamReader Reader, Engine engine);


    public static void AssetSerialize<T>(T? asset, StreamWriter Writer, Engine engine) where T : AssetBase, ISerializable, new()
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
    public static T? AssetDeserialize<T>(StreamReader Reader, Engine engine) where T : AssetBase, ISerializable, new()
    {
        var br = new BinaryReader(Reader.BaseStream);
        var len = br.ReadInt32();
        if (len == 0)
            return null;
        var str = br.ReadBytes(len);
        var Path = Encoding.UTF8.GetString(str);
        return engine.AssetMgr.Load<T>(Path);
    }

    public static string StringDeserialize(StreamReader Reader)
    {
        var br = new BinaryReader(Reader.BaseStream);
        var len = br.ReadInt32();
        if (len == 0)
            return string.Empty;
        var str = br.ReadBytes(len);
        return Encoding.UTF8.GetString(str);
    }

    public static void StringSerialize(string str, StreamWriter Writer)
    {
        var bw = new BinaryWriter(Writer.BaseStream);
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

}


public static class StreamHelper
{
    public static void Write(this BinaryWriter bw, Vector3 v)
    {
        bw.Write(BitConverter.GetBytes(v.X));
        bw.Write(BitConverter.GetBytes(v.Y));
        bw.Write(BitConverter.GetBytes(v.Z));
    }
    public static void Write(this BinaryWriter bw, Quaternion q)
    {
        bw.Write(BitConverter.GetBytes(q.X));
        bw.Write(BitConverter.GetBytes(q.Y));
        bw.Write(BitConverter.GetBytes(q.Z));
        bw.Write(BitConverter.GetBytes(q.W));
    }
    public static void Write(this BinaryWriter bw, in Matrix4x4 m)
    {
        bw.Write(BitConverter.GetBytes(m.M11));
        bw.Write(BitConverter.GetBytes(m.M12));
        bw.Write(BitConverter.GetBytes(m.M13));
        bw.Write(BitConverter.GetBytes(m.M14));

        bw.Write(BitConverter.GetBytes(m.M21));
        bw.Write(BitConverter.GetBytes(m.M22));
        bw.Write(BitConverter.GetBytes(m.M23));
        bw.Write(BitConverter.GetBytes(m.M24));

        bw.Write(BitConverter.GetBytes(m.M31));
        bw.Write(BitConverter.GetBytes(m.M32));
        bw.Write(BitConverter.GetBytes(m.M33));
        bw.Write(BitConverter.GetBytes(m.M34));

        bw.Write(BitConverter.GetBytes(m.M41));
        bw.Write(BitConverter.GetBytes(m.M42));
        bw.Write(BitConverter.GetBytes(m.M43));
        bw.Write(BitConverter.GetBytes(m.M44));
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
    public static int StaticMesh = 3;
    public static int SkeletalMesh = 4;
    public static int Material = 5;
    public static int Skeleton = 6;
    public static int AnimSequence = 7;

}