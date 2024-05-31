using SharpGLTF.Schema2;
using System.Numerics;

namespace Spark.Engine.Assets;

public class AnimSequence : AnimBase, IAssetBaseInterface
{
    public static int AssetMagicCode => MagicCode.AnimSequence;
    public Skeleton? Skeleton { get; set; }
    public AnimSequence()
    {
        ChannelsTransform = [];
        Channels = [];
    }
    public AnimSequence (string animName, float duration, Skeleton skeleton,Dictionary<int, BoneChannel> boneChannels)
    {
        this.AnimName = animName;
        this.Duration = duration;
        Channels = boneChannels;
        ChannelsTransform = new ();
        this.Skeleton = skeleton;
        InitTransform();
    }

    private void InitTransform()
    {
        foreach(var (id, channel) in Channels)
        {
            BoneTransform transforms = new ()
            {
                BoneId = channel.BoneId
            };
            for (var i = 0; i < channel.Translation.Count; i++)
            {
                var translation = channel.Translation[i].Item2;
                var rotation = channel.Rotation[i].Item2;
                var scale = channel.Scale[i].Item2;
                var matrix = MatrixHelper.CreateTransform(translation, rotation, scale);
                transforms.Transforms.Add((channel.Translation[i].Item1, matrix));
            }
            ChannelsTransform.Add(id, transforms);
        }
    }

    public override void Serialize(BinaryWriter bw, Engine engine)
    {
        bw.WriteInt32(MagicCode.Asset);
        bw.WriteInt32(AssetMagicCode);
        bw.WriteString2(AnimName);
        bw.WriteDouble(Duration);
        ISerializable.AssetSerialize(Skeleton, bw, engine);
        bw.WriteInt32(Channels.Count);
        foreach (var (id, channel) in Channels)
        {
            bw.WriteInt32(id);
            channel.Serialize(bw, engine);
        }

    }

    public override void Deserialize(BinaryReader br, Engine engine)
    {
        var assetMagicCode = br.ReadInt32();
        if (assetMagicCode != MagicCode.Asset)
            throw new Exception("");
        var textureMagicCode = br.ReadInt32();
        if (textureMagicCode != AssetMagicCode)
            throw new Exception("");
        AnimName = br.ReadString2();
        Duration = br.ReadDouble();
        Skeleton = ISerializable.AssetDeserialize<Skeleton>(br, engine);
        var count = br.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var id = br.ReadInt32();
            var channel = new BoneChannel();
            channel.Deserialize(br, engine);
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

    public void Deserialize(BinaryReader br, Engine engine)
    {
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

    public void Serialize(BinaryWriter writer, Engine engine)
    {
        var bw = new BinaryWriter(writer.BaseStream);
        bw.WriteInt32(BoneId);
        bw.WriteInt32(Translation.Count);
        foreach(var (time, translation) in Translation)
        {
            bw.WriteSingle(time);
            bw.Write(translation);
        }


        bw.WriteInt32(Rotation.Count);
        foreach (var (time, rotation) in Rotation)
        {
            bw.WriteSingle(time);
            bw.Write(rotation);
        }


        bw.WriteInt32(Scale.Count);
        foreach (var (time, scale) in Scale)
        {
            bw.WriteSingle(time);
            bw.Write(scale);
        }
    }
}


public class AnimSampler
{
    public AnimSequence Sequence;
    public Skeleton Skeleton;

    public List<Matrix4x4> TransformBuffer;

    public bool IsLoop = true;

    private double _speedTime;
    public AnimSampler(AnimSequence sequence)
    {
        Sequence = sequence;
        Skeleton = sequence.Skeleton;
        TransformBuffer = new List<Matrix4x4>(Skeleton.BoneList.Count);
        foreach(var bone in Skeleton.BoneList)
        {
            TransformBuffer.Add(Matrix4x4.Identity);
        }
        Clear();
    }

    public void Clear()
    {
        _speedTime = 0;

    }

    public double DurationScale = 1.0f;

    public double Duration => DurationScale * Sequence.Duration;

    public void Update(double deltaTime)
    {
        if (_speedTime >= Duration)
        {
            if (IsLoop == false)
                return;
            Clear();
        }
        foreach (var bone in Skeleton.BoneList)
        {
            var boneId = bone.BoneId;
            var transform = bone.RelativeTransform;
            if (Sequence.Channels.TryGetValue(boneId, out var channel))
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
                        if (_speedTime > channel.Translation[i].Item1 && _speedTime < channel.Translation[i + 1].Item1)
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
                    var dt = (float)_speedTime - channel.Translation[first].Item1;
                    var p = dt / len;
                    if (dt < 0)
                    {
                        p = 0;
                    }
                    transform = Matrix4x4.Lerp(transform1, transform2, p);
                }

            }
            TransformBuffer[boneId] = transform;
        }
        _speedTime += deltaTime / DurationScale;
    }


}