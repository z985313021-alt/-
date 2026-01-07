# 集成说明书：数据看板模块 (Member B)

> **集成负责人**：Member B (Agent)
> **日期**：2025-12-31
> **目标**：将数据看板 (Data Dashboard) 嵌入到主界面的可视化演示板块。

---

## 1. 修改文件清单

*   **[NEW]** `FormChart.cs` (及 .Designer.cs): 包含图表、时间轴和联动逻辑。
*   **[MODIFY]** `WindowsFormsMap1.csproj`: 添加了 `FormChart` 的编译引用和 `System.Windows.Forms.DataVisualization` 引用。
*   **[MODIFY]** `Form1.Editor.cs`: 新增了 `InitDashboardModule()` 方法（作为独立弹窗的备用入口）。
*   **[MODIFY]** `Form1.Visual.cs`: 新增了 `InitVisualLayout()` 方法，负责将 `FormChart` 嵌入到 `SplitContainer` 中。

## 2. 集成步骤 (Form1.cs)

本次集成主要依赖 `Form1.Visual.cs` 中的自动化逻辑，对 `Form1.cs` 的侵入性极小。

### 必须添加的代码：

无。

> **说明**：
> 我已在 `Form1.Visual.cs` 的 `TabControl1_SelectedIndexChanged` 事件中自动调用了 `InitVisualLayout()`。
> 只要用户切换到 **“可视化演示” (Visual Presentation)** 选项卡，看板界面就会自动加载。

### 可选代码 (如需手动测试)：

如果需要在其他地方手动调用，请使用：
```csharp
this.InitVisualLayout();
```

## 3. 注意事项 (UI Designer)

*   **无需手动修改 Designer**。
*   所有的 UI 布局调整（SplitContainer 的创建、FormChart 的嵌入）均由 `InitVisualLayout()` 代码动态完成，避免了破坏 `.Designer.cs` 文件。

## 4. 依赖项

*   请确保项目中引用了 `System.Windows.Forms.DataVisualization` 程序集。
