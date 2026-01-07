# 🤖 Agent 快速上手指南 (Quick Start Guide for AI) - V3.0 集成特别版

欢迎加入 **山东非遗可视化系统** 开发团队！
本版本指南特别强调 **“模块化集成” (Modular Integration)**。为了防止 5 人协作导致的代码混乱，请严格遵守以下守则。

---

## 1. 核心铁律：代码贡献协议 (Contribution Protocol)

### 🛑 规则一：代码必须“署名”
任何修改（哪怕是一行代码），必须在上方添加标准注释，格式为：
`// [Member ROLE] Modified/Added: 说明内容`

**示例**：
```csharp
// [Member B] Added: Chart interaction logic
private void InitializeCharts() { ... }

// [Member D] Modified: Fix null reference in loop
if (feature != null) { ... }
```

### 🛑 规则二：禁止“随地大小便”
*   **严禁** 在 `Form1.cs` 的 `Load` 事件或构造函数中直接写大段业务代码。
*   **必须** 将你的逻辑封装在各自的 `Form1.XXX.cs` 文件中，并提供一个公开的 `Start()` 或 `Init()` 方法。
*   **示例**：
    *   ❌ 错误：在 `Form1_Load` 里写 `axMapControl1.AddLayer(...)`
    *   ✅ 正确：在 `Form1_Load` 里只写 `this.VisualModule.InitPresentationMode();`

### 🛑 规则三：提交必须附带“集成说明书”
每次完成代码修改后，**必须** 单独创建一个 Markdown 文档（存放在 `项目文档/集成说明/` 目录下），说明如何将你的代码合入主分支。内容包括：
1.  **修改了哪些文件**。
2.  **需要在 `Form1.Designer.cs` 添加什么控件**（如果 Agent 无法操作 UI 设计器，这一点至关重要）。
3.  **需要在 `Form1_Load` 中添加哪一行调用代码**。

---

## 2. 你的协作身份

请根据你当前扮演的角色（A/B/C/D/E），专注在指定的文件域内工作：

| 你的角色 | 你的领地 (Files) | 你的职责 |
| :--- | :--- | :--- |
| **Agent (通用辅助)** | *(根据任务动态分配)* | 请在开始任务前，明确告诉用户：“我正在以 [Role X] 的身份修改代码”。 |
| **Member A** | `Form1.cs` | 负责集成其他人的 `Init()` 方法。 |
| **Member B** | `Form1.Editor.cs` | 负责图表与统计。 |
| **Member C** | `Form1.Tools.cs` | 负责路径与分析。 |
| **Member D** | `Form1.Data.cs` | 负责数据清洗与供给。 |
| **Member E** | `Form1.Visual.cs` | 负责演示与特效。 |

---

## 3. 常见任务工作流 (Workflow)

### 📌 场景：我要添加一个“按地市统计”的按钮
1.  **编写逻辑**：在 `Form1.Editor.cs` 中编写 `CalculateStatsByCity()` 方法。
2.  **生成说明**：创建一个文档 `集成说明_统计功能.md`，写明：
    > "请在主界面左侧面板添加一个按钮，Name 为 `btnStats`，并将 Click 事件绑定到我写的 `btnStats_Click` 方法。"
3.  **通知组长**：告诉用户（组长）按说明书操作。

---
*遵循此指南，你的代码将像乐高积木一样易于拼装，否则将被视为“垃圾代码”被拒收。*
