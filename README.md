# LiteEngine

## 背景

我入行游戏差不多两年了，今年终于从后台转到了客户端，因为我本来就对游戏的GamePlay有兴趣，所以更喜欢研究客户端的相关技术，现在终于可以光明正大的开始学习了 (以前总觉得自己不务正业QAQ)！

第一步就是好好学习一下渲染，我本科毕业设计的主题就是【FPS游戏引擎的设计与实现】，当时因为实力受限，最后交出了一份和基础的渲染代码，播了点模型动画混过了毕业，甚至在光照上还有些BUG(我的毕业设计[https://github.com/CeSun/Engine](https://github.com/CeSun/Engine))。

所以为了达成学习渲染这个目的，我打算使用C# 重构一下我的毕业设计，加入一些引擎真正应该有的功能，最终能基于此引擎开发一款五脏六腑俱全的游戏。
## 简介
这是一款基于OpenGL(ES)的使用纯C#编写的跨平台的游戏引擎框架，计划包含桌面端(Windows、Linux、MacOS)和移动端(Android、iOS)至少五种平台。

计划:
  1. 跨平台：安卓，桌面，专用服务器
  2. 移动平台适配摇杆
  3. GUI系统 (第三方)
  4. 物理引擎 (第三方)
  5. World, Actor, Pawn, Controller，Component等GamePlayer功能
  6. 网络同步
  7. 渲染方面：粒子系统，延迟渲染，水面反射/折射等特效
  8. 蒙皮动画以及动画控制器

**LiteEngine暂时不打算加入编辑器**

## 编译 & 使用 & 示例

> 由于引擎暂未开发完成，此部分暂无，感谢理解

## 开发 & 维护
[@CeSun](https://github.com/CeSun)

## 相关仓库

- [dotnet sdk](https://github.com/dotnet/sdk) — 先进的语言以及平台
- [silk.net](https://github.com/dotnet/Silk.NET) — OpenGL跨平台解决方案
- [StbSharp](https://github.com/rds1983/StbSharp) — 图片解析库

## 感谢
- [Learn OpenGL CN](https://learnopengl-cn.github.io/) 优秀的OpenGL教程
