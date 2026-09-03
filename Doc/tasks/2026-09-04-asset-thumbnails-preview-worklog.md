# E3 资源缩略图与预览工作日志

日期：2026-09-04

## 本轮目标

继续落实编辑器路线图 E3，先建立不依赖 GPU 的可复用缩略图缓存和资源编辑器预览基线，避免每次打开文档都重复生成
大图或上传完整 Texture。

## 完成内容

- 新增 `EditorAssetThumbnailCache` 和 `EditorAssetThumbnail`，缓存键包含 `AssetGuid + ContentHash + PreviewVersion`
  以及资源类型，支持显式按 AssetGuid 失效和整体清理。
- Texture 预览使用最近邻采样缩放到 96×96；Material 依据 BaseColor/BaseColorTexture 生成带透明背景的材质球近似；
  StaticMesh 依据顶点颜色生成稳定预览占位图。未知或未加载资源返回稳定棋盘占位图。
- StaticMesh、Material、Texture2D 资源编辑器均显示缓存预览；预览控件在标签关闭时释放 UI 纹理。
- 保持预览生成在编辑器 CPU/UI 边界内，不阻塞 RuntimeWorld，也不引入渲染线程依赖。

## 边界

- Content Browser 当前仍是文本列表；网格化缩略图、可见项按需请求和离屏 StaticMesh 相机渲染留在后续 E3 增量。
- Material 参数修改后的实时球面重绘、用户自定义预览场景和动画缩略图不在本轮范围。

## 验证

- 覆盖 Texture 缩放、缓存复用、ContentHash 失效、按 AssetGuid 清理。
- 覆盖 Material/StaticMesh 预览生成和透明背景。
- E3 相关测试与全量回归继续通过；全量测试 `283/283`，Demo Desktop 构建 0 警告、0 错误。
