using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core
{
    public class AnimationController : Component
    {
        public Skeleton AnimationSkeleton { get; private set; }
        public AnimationController(Skeleton animationSkeleton)
        {
            AnimationSkeleton = animationSkeleton;
        }
        Animation? Animation { get; set; }
        Skeleton? Skeleton { get; set; }

        public override void OnAttached(GameObject? Owner)
        {
            base.OnAttached(Owner);
            if (Owner is Model model)
            {
                foreach(var ( _, animation) in model.Animations)
                {
                    Animation = animation;
                }

                Skeleton = model.Skeleton;
                
        /*
                if (Skeleton != null && Skeleton.BoneAnimationMat != null)
                {
                    foreach(var (_, bone) in Skeleton.Bones)
                    {
                        Skeleton.BoneAnimationMat[bone.Id] = ProcessNode(bone);
                    }
                }
        */
            }

        }

        public override void OnRemoved(GameObject? OldOwner)
        {
            base.OnRemoved(OldOwner);
        }
        public override void Tick()
        {
            base.Tick();
            if (Animation == null)
                return;


        }

        /*
    public Matrix4 ProcessNode(BoneNode bone)
    {
        if (Skeleton == null)
            throw new Exception("ProcessNode");
        if (Skeleton.BoneOffsetMat == null)
            throw new Exception("ProcessNode");
        if (Skeleton.BoneAnimationMat == null)
            throw new Exception("ProcessNode");
        if (bone.Parent != null)
        {
            Skeleton.BoneOffsetMat[bone.Id] = bone.LocalTransform * ProcessNode(bone.Parent);
            Skeleton.BoneAnimationMat[bone.Id] = bone.OffsetTransform * Skeleton.BoneOffsetMat[bone.Id] ;
            return Skeleton.BoneOffsetMat[bone.Id];
        }
        Skeleton.BoneOffsetMat[bone.Id] = bone.LocalTransform;
        Skeleton.BoneAnimationMat[bone.Id] = bone.OffsetTransform * Skeleton.BoneOffsetMat[bone.Id];
        return Skeleton.BoneOffsetMat[bone.Id];
    }
        */
    }
}
