using SharpGLTF.Schema2;
using System.Numerics;

namespace Spark.Engine.Assets;

public class AnimSequence : AnimBase
{
    public Skeleton? Skeleton { get; set; }
    public AnimSequence()
    {
        ChannelsTransform = new();
        Channels = new();
    }
    public AnimSequence (string AnimName, float Duration, Skeleton Skeleton,Dictionary<int, BoneChannel> boneChannels)
    {
        this.AnimName = AnimName;
        this.Duration = Duration;
        Channels = boneChannels;
        ChannelsTransform = new ();
        this.Skeleton = Skeleton;
        InitTransform();
    }

    private void InitTransform()
    {
        foreach(var (id, channel) in Channels)
        {
            BoneTransform Transforms = new ();
            Transforms.BoneId = channel.BoneId;
            for (int i = 0; i < channel.Translation.Count; i++)
            {
                var translation = channel.Translation[i].Item2;
                var Rotation = channel.Rotation[i].Item2;
                var Scale = channel.Scale[i].Item2;
                var Matrix = MatrixHelper.CreateTransform(translation, Rotation, Scale);
                Transforms.Transforms.Add((channel.Translation[i].Item1, Matrix));
            }
            ChannelsTransform.Add(id, Transforms);
        }
    }

    public override void Serialize(StreamWriter Writer, Engine engine)
    {
        var bw = new BinaryWriter(Writer.BaseStream);
        bw.WriteInt32(MagicCode.Asset);
        bw.WriteInt32(MagicCode.AnimSequence);
        bw.WriteString2(AnimName);
        bw.WriteDouble(Duration);
        ISerializable.AssetSerialize(Skeleton, Writer, engine);
        bw.WriteInt32(Channels.Count);
        foreach (var (id, channel) in Channels)
        {
            bw.WriteInt32(id);
            channel.Serialize(Writer, engine);
        }

    }

    public override void Deserialize(StreamReader Reader, Engine engine)
    {
        var br = new BinaryReader(Reader.BaseStream);
        var AssetMagicCode = br.ReadInt32();
        if (AssetMagicCode != MagicCode.Asset)
            throw new Exception("");
        var TextureMagicCode = br.ReadInt32();
        if (TextureMagicCode != MagicCode.AnimSequence)
            throw new Exception("");
        AnimName = br.ReadString2();
        Duration = br.ReadSingle();
        Skeleton = ISerializable.AssetDeserialize<Skeleton>(Reader, engine);
        var count = br.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var id = br.ReadInt32();
            var channel = new BoneChannel();
            channel.Deserialize(Reader, engine);
            Channels.Add(id, channel);
        }
        InitTransform();
    }

    public double Duration { private set; get; }
    public string AnimName = string.Empty;
    public Dictionary<int, BoneChannel> Channels;
    public Dictionary<int, BoneTransform> ChannelsTransform;
}

public class BoneTransform
{
    public int BoneId;
    public List<(float, Matrix4x4)> Transforms = new List<(float, Matrix4x4)> ();
}
public class BoneChannel : ISerializable
{
    public int BoneId;

    public List<(float, Vector3)> Translation = new List<(float, Vector3)>();

    public List<(float, Quaternion)> Rotation = new List<(float, Quaternion)>();

    public List<(float, Vector3)> Scale = new List<(float, Vector3)>();

    public void Deserialize(StreamReader Reader, Engine engine)
    {
        var br = new BinaryReader(Reader.BaseStream);
        BoneId = br.ReadInt32();
        var count = br.ReadInt32();
        for(int i = 0; i < count; i++)
        {
            var time = br.ReadSingle();
            var translation = br.ReadVector3();
            Translation.Add((time, translation));
        }

        count = br.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var time = br.ReadSingle();
            var rotation = br.ReadQuaternion();
            Rotation.Add((time, rotation));
        }


        count = br.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var time = br.ReadSingle();
            var scale = br.ReadVector3();
            Scale.Add((time, scale));
        }
    }

    public void Serialize(StreamWriter Writer, Engine engine)
    {
        var bw = new BinaryWriter(Writer.BaseStream);
        bw.Write(BitConverter.GetBytes(BoneId));
        bw.Write(BitConverter.GetBytes(Translation.Count));
        foreach(var (time, translation) in Translation)
        {
            bw.Write(BitConverter.GetBytes(time));
            bw.Write(translation);
        }


        bw.Write(BitConverter.GetBytes(Rotation.Count));
        foreach (var (time, rotation) in Rotation)
        {
            bw.Write(BitConverter.GetBytes(time));
            bw.Write(rotation);
        }


        bw.Write(BitConverter.GetBytes(Scale.Count));
        foreach (var (time, scale) in Scale)
        {
            bw.Write(BitConverter.GetBytes(time));
            bw.Write(scale);
        }
    }
}


public class AnimSampler
{
    public AnimSequence Sequence;
    public Skeleton Skeleton;

    public List<Matrix4x4> TransfomrBuffer;

    public bool IsLoop = true;

    private double SpeedTime;
    public AnimSampler(AnimSequence sequence)
    {
        Sequence = sequence;
        Skeleton = sequence.Skeleton;
        TransfomrBuffer = new List<Matrix4x4>(Skeleton.BoneList.Count);
        foreach(var bone in Skeleton.BoneList)
        {
            TransfomrBuffer.Add(Matrix4x4.Identity);
        }
        Clear();
    }

    public void Clear()
    {
        SpeedTime = 0;

    }

    public double DurationScale = 1.0f;

    public double Duration => DurationScale * Sequence.Duration;

    public void Update(double DeltaTime)
    {
        if (SpeedTime >= Duration)
        {
            if (IsLoop == false)
                return;
            Clear();
        }
        foreach (var bone in Skeleton.BoneList)
        {
            var BoneId = bone.BoneId;
            var transform = bone.RelativeTransform;
            if (Sequence.Channels.TryGetValue(BoneId, out var channel))
            {
                if (Duration == 0)
                {
                    transform = MatrixHelper.CreateTransform(channel.Translation[0].Item2, channel.Rotation[0].Item2, channel.Scale[0].Item2);
                }
                else
                {
                    var first = 0;
                    var second = 0;
                    for (var i = 0; i < channel.Translation.Count - 1; i++)
                    {
                        if (SpeedTime > channel.Translation[i].Item1 && SpeedTime < channel.Translation[i + 1].Item1)
                        {
                            first = i;
                            second = first + 1;
                        }
                    }
                    if (first == second)
                    {
                        transform = MatrixHelper.CreateTransform(channel.Translation[first].Item2, channel.Rotation[first].Item2, channel.Scale[first].Item2);
                        continue;
                    }
                    var transform1 = MatrixHelper.CreateTransform(channel.Translation[first].Item2, channel.Rotation[first].Item2, channel.Scale[first].Item2);
                    var transform2 = MatrixHelper.CreateTransform(channel.Translation[second].Item2, channel.Rotation[second].Item2, channel.Scale[second].Item2);
                    var len = channel.Translation[second].Item1 - channel.Translation[first].Item1;
                    var dt = (float)SpeedTime - channel.Translation[first].Item1;
                    var p = dt / len;
                    if (dt < 0)
                    {
                        p = 0;
                    }
                    transform = Matrix4x4.Lerp(transform1, transform2, p);
                }

            }
            TransfomrBuffer[BoneId] = transform;
        }
        SpeedTime += DeltaTime / DurationScale;
    }


}