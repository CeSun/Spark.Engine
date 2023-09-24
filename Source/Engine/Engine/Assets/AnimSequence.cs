using Noesis;
using SharpGLTF.Schema2;
using System.Numerics;

namespace Spark.Engine.Assets;

public class AnimSequence : AnimBase
{
    public Skeleton Skeleton { get; set; }
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
public class BoneChannel
{
    public int BoneId;

    public List<(float, Vector3)> Translation = new List<(float, Vector3)>();

    public List<(float, Quaternion)> Rotation = new List<(float, Quaternion)>();

    public List<(float, Vector3)> Scale = new List<(float, Vector3)>();
}


public class AnimSampler
{
    public AnimSequence Sequence;
    public Skeleton Skeleton;

    public List<Matrix4x4> TransfomrBuffer;

    public bool IsLoop = false;

    private double SpeedTime;
    private int index;
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
        index = 0;

    }

    public double DurationScale = 1.0f;

    public double Duration => DurationScale * Sequence.Duration;

    public void Update(double DeltaTime)
    {
        ProcessBuffer(Skeleton.Root);
        SpeedTime += DeltaTime / DurationScale;
    }


    private void ProcessBuffer(BoneNode bone)
    {
        if (SpeedTime >= Duration)
        {
            if (IsLoop == false)
                return;
            Clear();
        }

        var index = bone.BoneId;
        var transform = bone.RelativeTransform;
        if (Sequence.ChannelsTransform.TryGetValue(index,  out var ChannelTransform))
        {
            //transform =  ChannelTransform.Transforms[0].Item2;
        }
        TransfomrBuffer[index] = transform;

        foreach (var child in bone.ChildrenBone)
        {
            ProcessBuffer(child);
        }



    }
}