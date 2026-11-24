# Dawn of Defense 白色战争迷雾

在团结 Hub 添加本目录，使用团结 1.9.3（2022.3.62t11）打开。运行场景 `Assets/Scenes/WhiteFog.unity`。工程使用团结内置 URP 14.2.0-t1 和线性色彩空间。

## 观看效果

进入 Play 后，基地位置按原版 1.5 秒曲线揭开。鼠标滚轮拉远可以同时看到雾内、雾边和雾外。灰色平面用于观察雾的透明度和深度交界。

| 操作 | 效果 |
| --- | --- |
| 左键点击地面 | 在点击位置完全开雾 |
| Shift + 左键 | 在点击位置切换为半可见 |
| 1 / 2 / 3 | 将最近一次点击的位置切为隐藏、半可见、完全可见 |
| 右键 | 让所有打开的区域重新合拢 |
| 滚轮 | 缩放观察距离 |
| 空格 | 暂停或恢复昼夜变化，云纹保持滚动 |
| 按住 [ 或 ] | 向前或向后调整昼夜相位 |

## 对应的原版资源

| 工程内容 | 原始来源 |
| --- | --- |
| FogOfWar.shader / FogOfWar.mat | sharedassets3.assets 中的 DOD/VFX/FogOfWar（408）和 FogOfWar（27）。前向顶点与片元程序按 D3D11 编译指令转写为 HLSL |
| FogMask.compute | sharedassets3.assets 中的 FogOfWar 计算程序（436）。三个可见度区间、重叠取最大值和遮罩边缘衰减均取自原程序 |
| NoiseClouds1 / NoiseClouds2 | sharedassets0.assets 中的 599 和 953，原始 2048×2048 R8 像素及 12 级 mip。A、B 两层共用第二张 |
| 两种雾面网格 | sharedassets3.assets 中的 272 和 328，沿用原始顶点、法线和三角索引 |
| OriginalFogPreset.asset | FogOfWarTransitionPreset（2893）和 TimeOfDayPreset_Default（2895）的可见度、半径、颜色和定向光强度关键帧 |
| FogOfWar.cs | level3 的实际配置。网格高度 5.8，遮罩在 200 个世界单位内使用 128×128 的 R16_SFloat，昼夜周期 800 秒 |

`FogPreview.cs` 提供独立场景的鼠标、键盘和镜头操作。复用雾效时可以直接调用 `FogOfWar.SetVisibility(xz, state, radius)`。state 的 0、1、2 依次表示隐藏、半可见和完全可见。当前场景使用默认昼夜预设，最终画面由独立预览相机渲染。原关卡地形和整屏后期会影响与游戏截图的对比。
