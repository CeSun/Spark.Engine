using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core;

public partial class SceneComponent
{
    public SceneComponent? Parent { get; private set; }
    private List<SceneComponent> Children { get; set; }
    public void AttachToComponent(SceneComponent TargetComponent)
    {
        if (Parent != null)
        {
            throw new Exception("该组件已经附加到其他组件上了！");
        }
        Parent = TargetComponent;
        Parent.Children.Add(this);
    }

    public void DettachFromComponent()
    {
        if (Parent == null)
        {
            return;
        }
        Parent.Children.Remove(this);
        Parent = null;
    }

    
    private Vector3 _WorldLocation;
    public Vector3 WorldLocation 
    {
        get 
        {
            UpdateTransform();
            return _WorldLocation;
        }
        set
        {
            var LocationMatrix = Matrix4x4.CreateTranslation(value);
            var RotationMatrix = Matrix4x4.CreateFromQuaternion(WorldRotation);
            var ScaleMatrix = Matrix4x4.CreateScale(WorldScale);
            // 计算世界矩阵
            var Transform = ScaleMatrix * RotationMatrix * LocationMatrix;
            // 父组件矩阵
            var ParentTransform = (Parent == null ? Matrix4x4.Identity : Parent.WorldTransfrom);
            // 父组件逆矩阵
            Matrix4x4.Invert(ParentTransform, out var InvertParentTransform);
            // 从世界矩阵回到相对矩阵
            var RelativeTransform = Transform * InvertParentTransform;
            // 计算出相对位移
            RelativeLocation = RelativeTransform.Translation;
        }
    }
    private Vector3 _WorldScale;
    public Vector3 WorldScale
    {
        get
        {
            UpdateTransform();
            return _WorldScale;
        }
        set
        {
            var LocationMatrix = Matrix4x4.CreateTranslation(WorldLocation);
            var RotationMatrix = Matrix4x4.CreateFromQuaternion(WorldRotation);
            var ScaleMatrix = Matrix4x4.CreateScale(value);
            // 计算世界矩阵
            var Transform = ScaleMatrix * RotationMatrix * LocationMatrix;
            // 父组件矩阵
            var ParentTransform = (Parent == null ? Matrix4x4.Identity : Parent.WorldTransfrom);
            // 父组件逆矩阵
            Matrix4x4.Invert(ParentTransform, out var InvertParentTransform);
            // 从世界矩阵回到相对矩阵
            var RelativeTransform = Transform * InvertParentTransform;
            // 计算出相对缩放
            RelativeScale = RelativeTransform.Scale();
        }
    }
    private Quaternion _WorldRotation;
    public Quaternion WorldRotation
    {
        get
        {
            UpdateTransform();
            return _WorldRotation;
        }
        set
        {
            var LocationMatrix = Matrix4x4.CreateTranslation(WorldLocation);
            var RotationMatrix = Matrix4x4.CreateFromQuaternion(value);
            var ScaleMatrix = Matrix4x4.CreateScale(WorldScale);
            // 计算世界矩阵
            var Transform = ScaleMatrix * RotationMatrix * LocationMatrix;
            // 父组件矩阵
            var ParentTransform = (Parent == null ? Matrix4x4.Identity : Parent.WorldTransfrom);
            // 父组件逆矩阵
            Matrix4x4.Invert(ParentTransform, out var InvertParentTransform);
            // 从世界矩阵回到相对矩阵
            var RelativeTransform = Transform * InvertParentTransform;
            // 计算出相对旋转
            RelativeRotation = RelativeTransform.Rotation();
        }
    }
    private Matrix4x4 _WorldTransfrom;
    public Matrix4x4 WorldTransfrom 
    { 
        get
        {
            UpdateTransform();
            return _WorldTransfrom;
        }
        private set => _WorldTransfrom = value; 
    }

    private Vector3 _RelativeLocation;
    public Vector3 RelativeLocation 
    {
        get => _RelativeLocation;
        set 
        {
            TransformDirtyFlag = true;
            _RelativeLocation = value;
        } 
    }
    Vector3 _RelativeScale = Vector3.One;
    public Vector3 RelativeScale 
    {
        get => _RelativeScale;
        set
        {
            TransformDirtyFlag = true;
            _RelativeScale = value;
        } 
    }

    Quaternion _RelativeRotation;
    public Quaternion RelativeRotation 
    {
        get => _RelativeRotation;
        set
        {
            TransformDirtyFlag = true;
            _RelativeRotation = value;
        }
    }
    Matrix4x4 _RelativeTransform;
    public Matrix4x4 RelativeTransform 
    { 
        get
        {
            UpdateTransform();
            return _RelativeTransform;
        }
        private set => _RelativeTransform = value; 
    }

    private bool _TransformDirtyFlag;
    public bool TransformDirtyFlag 
    {
        get => _TransformDirtyFlag;
        set
        {
            Children.ForEach(component => component.TransformDirtyFlag = value);
            _TransformDirtyFlag = value;
        }
    }
    public void UpdateTransform()
    {
        if (TransformDirtyFlag)
        {
            // 创建位移矩阵
            var LocationMatrix = Matrix4x4.CreateTranslation(RelativeLocation);
            // 创建旋转矩阵
            var RotationMatrix = Matrix4x4.CreateFromQuaternion(RelativeRotation);
            // 创建缩放矩阵
            var ScaleMatrix = Matrix4x4.CreateScale(RelativeScale);
            // 计算相对矩阵, 矩阵是行主序的
            RelativeTransform = ScaleMatrix * RotationMatrix * LocationMatrix;
            // 计算到世界空间矩阵
            WorldTransfrom = (Parent == null ? Matrix4x4.Identity : Parent.WorldTransfrom) * RelativeTransform;
            // 清楚脏数据标记
            _TransformDirtyFlag = false;
            // 计算世界坐标
            _WorldLocation = WorldTransfrom.Translation;
            // 计算世界空间缩放
            _WorldScale = WorldTransfrom.Scale();
            // 计算世界空间缩放
            _WorldRotation = WorldTransfrom.Rotation();

        }
    }




}
