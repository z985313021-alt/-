-- [Member E] Added: 路线保存功能数据库表
-- 用于保存用户规划的路线和关联的非遗点位

USE [YourDatabaseName]; -- 请替换为实际数据库名
GO

-- 1. 保存的路线表（优化：只保存起终点，加载时重新计算路径）
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Saved_Routes')
BEGIN
    CREATE TABLE Saved_Routes (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        RouteName NVARCHAR(100) NOT NULL,
        StartLng FLOAT NOT NULL,
        StartLat FLOAT NOT NULL,
        EndLng FLOAT NOT NULL,
        EndLat FLOAT NOT NULL,
        -- PathCoordinates 字段已移除：加载时重新计算路径更高效
        CreatedDate DATETIME DEFAULT GETDATE(),
        Description NVARCHAR(500)
    );
    
    PRINT '✓ 已创建 Saved_Routes 表（优化版：不存完整路径）';
END
ELSE
BEGIN
    PRINT '⚠ Saved_Routes 表已存在，跳过创建';
END
GO

-- 2. 路线关联的非遗点位表（简化：只存名称，避免外键依赖）
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Route_ICH_Items')
BEGIN
    CREATE TABLE Route_ICH_Items (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        RouteID INT NOT NULL,
        ICH_Name NVARCHAR(200), -- 非遗项目名称（冗余存储，避免外键依赖）
        SequenceOrder INT, -- 在路线中的顺序
        DistanceFromPath FLOAT, -- 距离路径的距离（度）
        CreatedDate DATETIME DEFAULT GETDATE(),
        
        -- 外键约束
        CONSTRAINT FK_RouteICH_Route FOREIGN KEY (RouteID) 
            REFERENCES Saved_Routes(ID) ON DELETE CASCADE
    );
    
    PRINT '✓ 已创建 Route_ICH_Items 表（简化版）';
END
ELSE
BEGIN
    PRINT '⚠ Route_ICH_Items 表已存在，跳过创建';
END
GO

-- 3. 创建索引优化查询性能
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDX_SavedRoutes_Date')
BEGIN
    CREATE INDEX IDX_SavedRoutes_Date ON Saved_Routes(CreatedDate DESC);
    PRINT '✓ 已创建索引 IDX_SavedRoutes_Date';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDX_RouteICH_RouteID')
BEGIN
    CREATE INDEX IDX_RouteICH_RouteID ON Route_ICH_Items(RouteID);
    PRINT '✓ 已创建索引 IDX_RouteICH_RouteID';
END
GO

PRINT '';
PRINT '========================================';
PRINT '数据库表创建完成！';
PRINT '========================================';
PRINT '请在 Form1.Visual.cs 中添加对应的 C# 方法';
GO
