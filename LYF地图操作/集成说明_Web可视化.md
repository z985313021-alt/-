# 集成说明书：Web可视化演示模式

## 1. 修改摘要

本次更新新增了一个独立窗口 `FormWebVisual`，用于展示基于 Web 技术 (HTML/CSS/JS) 的酷炫可视化界面。该模块独立于 ArcGIS Engine，使用 WebView2 控件。

### 新增文件

- **Web 资源**: `VisualWeb/` 目录 (包含 `index.html`, `css/`, `js/`, `data/`)
- **窗口文件**: `FormWebVisual.cs`, `FormWebVisual.Designer.cs`
- **文档**: `集成说明_Web可视化.md`

### 修改文件

- **项目文件**: `WindowsFormsMap1.csproj` (已自动添加新文件引用)
- **主逻辑**: `Form1.Visual.cs` (在侧边栏新增 "WEB" 按钮，并实现 `OpenWebVisualMode` 方法)

## 2. 集成步骤 (Automated)

Agent 已自动完成所有代码集成工作，无需手动操作 Designer。

- **WinForms 集成**: 侧边栏索引 4 处已自动插入 "Web演示" 按钮。
- **依赖检查**: 项目已包含 `Microsoft.Web.WebView2` 包，无需额外安装。

## 3. 验证方法

1. **启动程序**: 运行 `WindowsFormsMap1`。
2. **进入演示模式**: 点击顶部 Tab 的“可视化演示”或等待自动进入。
3. **点击按钮**: 在左侧白色侧边栏中，点击倒数第二个按钮 (图标为地球仪/Web网络)。
4. **观察结果**:
    - 应弹出一个全新的全屏黑色窗口。
    - 窗口背景有粒子连线动画。
    - 界面显示“山东省非物质文化遗产大数据概览”。
    - 数据面板应动态显示 Mock 数据 (如济南 24 项)。

## 4. 注意事项

- **WebView2 运行时**: 运行此功能的机器必须安装 [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (Windows 10/11 通常自带)。如果弹窗提示错误，请安装运行时。
- **文件路径**: Web 资源文件位于项目根目录的 `VisualWeb` 文件夹。
