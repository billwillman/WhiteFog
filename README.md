# 白色战争迷雾

在团结 Hub 添加本目录，使用团结 1.9.3（2022.3.62t11）打开。工程使用团结内置 URP 14.2.0-t1 和线性色彩空间。

## 双平台演示

### 开雾与镜头缩放

| PC 原画质 | 微信小游戏平衡档 |
| --- | --- |
| [![PC 原画质开雾与镜头缩放演示](docs/previews/WhiteFog-PC.webp)](WhiteFog-PC.mp4) | [![微信平衡档开雾与镜头缩放演示](docs/previews/WhiteFog-WeChat.webp)](WhiteFog-WeChat.mp4) |
| [完整 MP4](WhiteFog-PC.mp4) · [PC 演示场景](Assets/Scenes/WhiteFog.unity) | [完整 MP4](WhiteFog-WeChat.mp4) · [微信演示场景](Assets/Scenes/WhiteFog_WeChat.unity) |

两段开雾演示均为团结编辑器实录，各 12 秒，镜头和操作顺序相同。

### 天气颜色变化

固定镜头和开雾范围，用 8 秒走完原本 800 秒的昼夜雾色周期。留意雾边从夜间冷色转为晨光暖金、日间灰白和晚霞暖色，再回到夜色。两段均为团结编辑器实录，使用相同的预设和时间进度。

| PC 原画质 | 微信小游戏平衡档 |
| --- | --- |
| [![PC 天气雾色变化](docs/previews/WhiteFog-Weather-PC.webp)](WhiteFog-Weather-PC.mp4) | [![微信天气雾色变化](docs/previews/WhiteFog-Weather-WeChat.webp)](WhiteFog-Weather-WeChat.mp4) |
| [完整 MP4](WhiteFog-Weather-PC.mp4) | [完整 MP4](WhiteFog-Weather-WeChat.mp4) |

### 运行演示

切换构建目标时，工程自动选择对应演示场景。微信小游戏（MiniGame）和 WebGL 使用移动场景及 WebGL 2，Standalone 使用 PC 场景。可以在桌面编辑器中直接打开任一场景预览。手机上轻点完全开雾，长按松开切换半可见，双指捏合缩放。

PC 场景使用原计算着色器、高密度网格和两张 2048 噪声纹理。移动场景采用 128×128 的 CPU R8 遮罩，变化时最多每秒上传 15 次。每个开雾点按原公式独立合并三段可见度，遮罩与世界坐标原点同步更新。移动材质使用两张保留 mip 的 1024 R8 噪声纹理，顶点采一次云纹，片元采两次，逐帧滚动，保留场景深度软交界。

移动雾面覆盖 192×192 个世界单位，中心网格间距为 1，外围为 2，两者边界共用顶点。按 16×16 世界单位分块，使用包含上下位移的包围盒与当前相机视锥相交测试。只有可见块集合改变时才更新索引，同一相机的可见雾面合为一次绘制。

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
