# ===================================================================
# 非遗图片批量重命名脚本
# 功能：将图片文件名标准化为与数据库项目名称完全一致
# 作者：Antigravity AI
# ===================================================================

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Continue"

# 配置区
$ImagesRootPath = ".\LYF地图操作\23110908016王亮\WindowsFormsMap1\VisualWeb\images"
$SqlServer = ".\SQLEXPRESS"
$Database = "ICH_VisualDB"

Write-Host "=== 非遗图片批量重命名工具 ===" -ForegroundColor Cyan
Write-Host ""

# 步骤1：从数据库获取标准项目名称
Write-Host "[1/4] 正在从数据库获取标准项目名称..." -ForegroundColor Yellow
$Query = "SELECT DISTINCT Name, Category FROM ICH_Items ORDER BY Name"
$ProjectNames = Invoke-Sqlcmd -ServerInstance $SqlServer -Database $Database -Query $Query -ErrorAction Stop

Write-Host "      找到 $($ProjectNames.Count) 个项目" -ForegroundColor Green
Write-Host ""

# 步骤2：扫描所有图片文件
Write-Host "[2/4] 正在扫描图片文件..." -ForegroundColor Yellow
$AllImageFiles = Get-ChildItem -Path $ImagesRootPath -Recurse -Include *.jpg,*.jpeg,*.png,*.webp -File
Write-Host "      找到 $($AllImageFiles.Count) 个图片文件" -ForegroundColor Green
Write-Host ""

# 步骤3：生成重命名映射
Write-Host "[3/4] 正在分析文件名匹配..." -ForegroundColor Yellow
$RenameActions = @()

foreach ($imageFile in $AllImageFiles) {
    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($imageFile.Name)
    $extension = $imageFile.Extension
    $folder = Split-Path $imageFile.DirectoryName -Leaf
    
    # 提取基础名称（去除序号，如 "吕剧1" -> "吕剧"）
    $baseName = $fileName -replace '\d+$', ''
    
    # 查找匹配的数据库项目
    $matchedProject = $ProjectNames | Where-Object { 
        $_.Name -eq $baseName -or 
        $_.Name -like "$baseName*" -or
        $fileName -like "*$($_.Name)*"
    } | Select-Object -First 1
    
    if ($matchedProject) {
        $standardName = $matchedProject.Name
        
        # 检查是否有序号（多图场景）
        if ($fileName -match '(\d+)$') {
            $序号 = $Matches[1]
            $newName = "$standardName$序号$extension"
        } else {
            $newName = "$standardName$extension"
        }
        
        # 如果新旧文件名不同，则记录重命名动作
        if ($imageFile.Name -ne $newName) {
            $RenameActions += [PSCustomObject]@{
                旧文件 = $imageFile.FullName
                新文件名 = $newName
                文件夹 = $folder
                匹配项目 = $standardName
            }
        }
    } else {
        Write-Host "  [警告] 未找到匹配项目: $fileName (位于 $folder)" -ForegroundColor DarkYellow
    }
}

Write-Host "      需要重命名 $($RenameActions.Count) 个文件" -ForegroundColor Green
Write-Host ""

# 步骤4：显示预览并确认
if ($RenameActions.Count -eq 0) {
    Write-Host "所有文件名已符合标准，无需重命名！" -ForegroundColor Green
    exit 0
}

Write-Host "[4/4] 重命名预览（前10项）：" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Gray
$RenameActions | Select-Object -First 10 | ForEach-Object {
    $oldName = Split-Path $_.旧文件 -Leaf
    Write-Host "  [$($_.文件夹)] $oldName" -ForegroundColor White
    Write-Host "    ->  $($_.新文件名)" -ForegroundColor Cyan
}
if ($RenameActions.Count -gt 10) {
    Write-Host "  ... 以及其他 $($RenameActions.Count - 10) 个文件" -ForegroundColor Gray
}
Write-Host "----------------------------------------" -ForegroundColor Gray
Write-Host ""

# 用户确认
$confirmation = Read-Host "是否执行重命名？(输入 Y 继续，其他键取消)"
if ($confirmation -ne 'Y' -and $confirmation -ne 'y') {
    Write-Host "已取消操作。" -ForegroundColor Yellow
    exit 0
}

# 执行重命名
Write-Host ""
Write-Host "正在执行重命名..." -ForegroundColor Yellow
$successCount = 0
$failCount = 0

foreach ($action in $RenameActions) {
    try {
        $newPath = Join-Path (Split-Path $action.旧文件 -Parent) $action.新文件名
        Rename-Item -Path $action.旧文件 -NewName $action.新文件名 -ErrorAction Stop
        $successCount++
    } catch {
        Write-Host "  [错误] 重命名失败: $($action.旧文件)" -ForegroundColor Red
        Write-Host "         原因: $($_.Exception.Message)" -ForegroundColor Red
        $failCount++
    }
}

Write-Host ""
Write-Host "=== 完成 ===" -ForegroundColor Cyan
Write-Host "  成功: $successCount 个文件" -ForegroundColor Green
if ($failCount -gt 0) {
    Write-Host "  失败: $failCount 个文件" -ForegroundColor Red
}
Write-Host ""
Write-Host "提示：现在可以刷新Web页面，图片应该能正常加载了！" -ForegroundColor Yellow
