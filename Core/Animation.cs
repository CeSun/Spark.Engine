using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Mathematics;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core
{
    public class Animation
    {
        public Animation(string name, double Time)
        {
            Nodes = new Dictionary<string, AnimationNode>();
            Name = name;
            this.Time = Time;
        }

        public double Time { get; set; }
        public string Name { get; private set; }
        public Dictionary<string, AnimationNode> Nodes { get; private set; }

        // 可以考虑缓存一下
        public (Vector3 position, Quaternion rotation, Vector3 scale)GetAnimationByNodeNameAndTime(string node, double time)
        {
            if (Nodes.TryGetValue(node, out var animationNode))
            {
                return animationNode.GetValueByTime(time);
            }
            throw new Exception($"{node}: 不存在");
        }
    }

    
    public struct AnimationNodeKey<T> where T : struct
    {
        public T Value { get; set; }
        public double Time { get; set; }

    }

    public class AnimationNode
    {
        public AnimationNode(string name)
        {
            Name = name;
            PositionKeys = new List<AnimationNodeKey<Vector3>>();
            ScaleKeys = new List<AnimationNodeKey<Vector3>>();
            RotationKeys = new List<AnimationNodeKey<Quaternion>>();
        }
        public List<AnimationNodeKey<Vector3>> PositionKeys { get; private set; }
        public List<AnimationNodeKey<Vector3>> ScaleKeys { get; private set; }
        public List<AnimationNodeKey<Quaternion>> RotationKeys { get; private set; }
        public string Name { get; private set; }

        // 垃圾重复代码太多了，需要重构
        public (Vector3 position, Quaternion rotation, Vector3 scale) GetValueByTime(double Time)
        {
            int index = 0;
            int left = 0;
            int right = 0;
            foreach(var key in PositionKeys)
            {
                if (key.Time > Time)
                {
                    left = index;
                    if (left == 0)
                        right = 0;
                    break;
                }
                index++;
            }
            if (index == PositionKeys.Count)
            {
                left = PositionKeys.Count - 1;
                right = left;
            }
            var deltaTime = Time -  PositionKeys[left].Time;
            var allTime = PositionKeys[right].Time - PositionKeys[left].Time;
            var weight = deltaTime / allTime;

            var outPosition = (PositionKeys[left].Value + (PositionKeys[right].Value- PositionKeys[left].Value) * (float)weight);


            index = 0;
            left = 0;
            right = 0;
            foreach (var key in ScaleKeys)
            {
                if (key.Time > Time)
                {
                    left = index;
                    if (left == 0)
                        right = 0;
                    break;
                }
                index++;
            }
            if (index == ScaleKeys.Count)
            {
                left = ScaleKeys.Count - 1;
                right = left;
            }
            deltaTime = Time - ScaleKeys[left].Time;
            allTime = ScaleKeys[right].Time - ScaleKeys[left].Time;
            weight = deltaTime / allTime;
            var outScaleKey = (ScaleKeys[left].Value + (ScaleKeys[right].Value - ScaleKeys[left].Value) * (float)weight);

            index = 0;
            left = 0;
            right = 0;
            foreach (var key in RotationKeys)
            {
                if (key.Time > Time)
                {
                    left = index;
                    if (left == 0)
                        right = 0;
                    break;
                }
                index++;
            }
            if (index == RotationKeys.Count)
            {
                left = RotationKeys.Count - 1;
                right = left;
            }
            deltaTime = Time - RotationKeys[left].Time;
            allTime = RotationKeys[right].Time - RotationKeys[left].Time;
            weight = deltaTime / allTime;

            var outRotation = (RotationKeys[left].Value * Quaternion.Slerp(RotationKeys[right].Value, RotationKeys[left].Value, (float)weight)); ;



            return (outPosition, outRotation, outScaleKey);
        }


       
    }
}
