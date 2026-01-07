# 山东非遗可视化系统 - UI 大屏重构集成说明书

**Member ROLE**: Member E (演示与特效)
**版本**: v1.0

---

## 1. 修改文件清单
本次修改涉及以下文件，均已遵循 `// [Member ROLE]` 署名守则：
- `Form1.cs`: 调整 `Form1_Load` 启动逻辑，设置默认选中演示选项卡。
- `Form1.Visual.cs` [NEW/RESTORED]: 核心重构逻辑，实现了“左侧单栏 - 右侧内容区”架构。
- `UIHelper.cs`: 添加了 `CloneMap` 静态方法，用于在不同模式地图间同步数据。
- `FormChart.cs` (此前修改): 修复了图表点击定位逻辑。

---

## 2. UI 设计器手动调整项 (Form1.Designer.cs)

> [!IMPORTANT]
> 由于本重构在运行时动态调整布局，您**不需要**在设计器中手动添加新 Panel，但请确保以下控件名称正确。

### 核心控件核对：
- **TabControl**: `tabControl1`
- **TabPage**: `tabPageVisual` (索引建议为 2)
- **MapControl**: `axMapControlVisual` (应位于 `tabPageVisual` 内)

---

## 3. 代码集成步骤 (关键点)

### 步骤 A：Form1.cs 构造或 Load 初始化
请确保在 `Form1_Load` 中包含以下调用（已在当前代码中实现）：
```csharp
// 1. 同步加载 MXD
LoadDefaultMxd();

// 2. 切换到演示选项卡索引 (会自动触发 TabControl1_SelectedIndexChanged)
this.tabControl1.SelectedIndex = 2; 
```

### 步骤 B：分部类集成
确保 `Form1.Visual.cs` 已包含在项目中。该类负责：
1. 在 `TabControl1_SelectedIndexChanged` 中检测到演示模式时调用 `SetUIMode(true)`。
2. 调用 `InitVisualLayout()` 重新编排界面（将 `axMapControlVisual` 和 `FormChart` 移入动态创建的容器）。

---

## 4. 预览效果
重构后，系统启动将直接展示暗色调大屏，左侧提供“全景”、“分类”、“地市”、“演变”快捷导航。
- **全景**: 自动缩放至山东全省。
- **分类**: 启动非遗项目分类渲染。
- **地市**: 激活底部数据看板。
- **演变**: 进入时空演示模式（逻辑框架已就绪）。

---
*请组长（A）确认合入逻辑，如有异常请检查 ArcGIS 类库引用是否完整。*
