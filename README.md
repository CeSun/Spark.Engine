# Spark Engine 简介

欢迎访问Spark引擎的代码仓库！

Spark引擎是一个使用Opnegl开发的开源游戏引擎(虽然也没法商用)，主要目的是为了将我学过的知识或者写过的小案例进行整合并验证的项目，因此Spark引擎看起来十分的简陋，甚至无法用于任何正式或者非正式的项目中，但无所谓，只要我学到全新的知识我都会尝试加入到Spark引擎中。

![运行截图](/Images/ScreenShot1.png "屏幕空间反射")
![运行截图](/Images/ScreenShot3.png "实例化渲染")
![运行截图](/Images/SSAO.png "物理引擎和环境光遮蔽")

# 已完成功能

- [x] 基于Actor和Component的场景管理
- [x] 跨平台的封装 (桌面和安卓)
- [x] 布林冯渲染器
- [x] 三种透光物: 点光源，定向光源，投射光源
- [x] 延迟着色
- [x] 基于屏幕空间的反射  
- [x] Bloom泛光效果
- [x] 法线贴图
- [x] 时差贴图
- [x] 静态模型渲染
- [x] 层级实例化渲染
- [x] 实例化渲染
- [x] 接入物理系统
- [x] 延迟贴花 
- [x] 环境光遮蔽

# 正在做的功能

- [ ] GBuffer压缩
- [ ] 基于物理的渲染（PBR）

# 计划要做的功能

- [ ] 自定义Shader
- [ ] 伽马矫正
- [ ] 顺序无关的透明片渲染
- [ ] 动画控制器
- [ ] 骨骼动画
- [ ] IMGUI

# 遥远的畅想

- [ ] 专用服务器
- [ ] 状态同步
- [ ] Game Play框架
- [ ] 游戏编辑器

# 感谢

1. dotnet: 优秀的跨平台运行时 [https://github.com/dotnet/runtime](https://github.com/dotnet/runtime)
2. Silk.Net: 集图形，声音等高性能的低级api绑定库 [https://github.com/dotnet/Silk.NET](https://github.com/dotnet/Silk.NET)
3. StbImageSharp: stb 图像库的绑定 [https://github.com/StbSharp/StbImageSharp](https://github.com/StbSharp/StbImageSharp)
4. SharpGLTF: 解析gltf格式模型的库 [https://github.com/vpenades/SharpGLTF](https://github.com/vpenades/SharpGLTF)
5. JitterPhysics: 纯C#的物理引擎 [https://github.com/notgiven688/jitterphysics](https://github.com/notgiven688/jitterphysics)
