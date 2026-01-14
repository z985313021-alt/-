// [Agent (通用辅助)] Modified: 中文化注释与架构梳理
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.DataSourcesFile;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.esriSystem;

namespace WindowsFormsMap1
{
    public class DataHelper
    {
        /// <summary>
        /// 【数据瘦身】：利用空间过滤技术，从全国的海量非遗数据中筛选出山东省的项目，并转存为高性能的 File GDB 格式
        /// </summary>
        /// <param name="sourceShpPath">原始全国 Shapefile 的完整路径</param>
        /// <param name="targetGdbDir">目标地理数据库所在的文件夹</param>
        /// <param name="targetGdbName">生成的 GDB 名称（建议以 .gdb 结尾）</param>
        public static string SlimDataToGDB(string sourceShpPath, string targetGdbDir, string targetGdbName)
        {
            try
            {
                // 1. 创建/打开工作空间
                IWorkspace targetWorkspace = CreateOrOpenFileGDB(targetGdbDir, targetGdbName);
                if (targetWorkspace == null) return "无法创建或打开目标数据库";

                // 2. 打开原始 Shapefile
                IFeatureClass sourceFeatureClass = OpenShapefile(sourceShpPath);
                if (sourceFeatureClass == null) return "无法打开原始 Shapefile";

                // 3. 构建 SQL 过滤条件（仅提取省份字段为“山东省”的记录）
                IQueryFilter queryFilter = new QueryFilterClass();
                queryFilter.WhereClause = "省 = '山东省'";

                // 4. 定义目标要素类的名称（原名加 _SD 后缀）
                string className = System.IO.Path.GetFileNameWithoutExtension(sourceShpPath) + "_SD";

                // 安全清理：如果目标数据库中已存在同名要素类，先将其删除以避免冲突
                if ((targetWorkspace as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureClass, className))
                {
                    IDataset dataset = (targetWorkspace as IFeatureWorkspace).OpenFeatureClass(className) as IDataset;
                    dataset.Delete();
                }

                // 5. 执行数据导出 (瘦身)
                // 使用 IFeatureDataConverter 或简单的 循环插入
                ExportFilteredFeatures(sourceFeatureClass, targetWorkspace as IFeatureWorkspace, className, queryFilter);

                return $"成功！数据已瘦身并存入：{System.IO.Path.Combine(targetGdbDir, targetGdbName)}\\{className}";
            }
            catch (Exception ex)
            {
                return "错误: " + ex.Message;
            }
        }

        private static IWorkspace CreateOrOpenFileGDB(string path, string name)
        {
            IWorkspaceFactory workspaceFactory = new FileGDBWorkspaceFactoryClass();
            string fullPath = System.IO.Path.Combine(path, name);

            if (!System.IO.Directory.Exists(fullPath))
            {
                IWorkspaceName workspaceName = workspaceFactory.Create(path, name, null, 0);
                IName nameObj = (IName)workspaceName;
                return (IWorkspace)nameObj.Open();
            }
            else
            {
                return workspaceFactory.OpenFromFile(fullPath, 0);
            }
        }

        private static IFeatureClass OpenShapefile(string fullPath)
        {
            string dir = System.IO.Path.GetDirectoryName(fullPath);
            string file = System.IO.Path.GetFileName(fullPath);

            IWorkspaceFactory workspaceFactory = new ShapefileWorkspaceFactoryClass();
            IFeatureWorkspace featureWorkspace = (IFeatureWorkspace)workspaceFactory.OpenFromFile(dir, 0);
            return featureWorkspace.OpenFeatureClass(file);
        }

        private static void ExportFilteredFeatures(IFeatureClass sourceFC, IFeatureWorkspace targetFW, string newName, IQueryFilter filter)
        {
            // 1. 创建目标要素类的字段集（结构基本继承自源数据）
            IFields targetFields = CloneFields(sourceFC.Fields);

            // 2. 字段增强：如果原始数据缺失“类别”字段，则手动补全，方便后续的专题图渲染
            if (sourceFC.FindField("类别") == -1)
            {
                IFieldsEdit fieldsEdit = (IFieldsEdit)targetFields;
                IField categoryField = new FieldClass();
                IFieldEdit fieldEdit = (IFieldEdit)categoryField;
                fieldEdit.Name_2 = "类别";
                fieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
                fieldEdit.Length_2 = 50;
                fieldEdit.AliasName_2 = "项目类别";
                fieldsEdit.AddField(categoryField);
            }

            IFeatureClass targetFC = targetFW.CreateFeatureClass(newName, targetFields, sourceFC.CLSID, sourceFC.EXTCLSID, sourceFC.FeatureType, sourceFC.ShapeFieldName, "");

            // 循环插入 (瘦身核心)
            IFeatureCursor sourceCursor = sourceFC.Search(filter, true);
            IFeature sourceFeature = sourceCursor.NextFeature();

            IFeatureCursor insertCursor = targetFC.Insert(true);

            int count = 0;
            while (sourceFeature != null)
            {
                IFeatureBuffer featureBuffer = targetFC.CreateFeatureBuffer();
                // 拷贝属性
                for (int i = 0; i < sourceFeature.Fields.FieldCount; i++)
                {
                    IField sourceField = sourceFeature.Fields.get_Field(i);
                    // 找到目标要素类中对应的索引 (因为增加了新字段，索引可能会位移)
                    int targetIdx = targetFC.FindField(sourceField.Name);
                    if (targetIdx != -1)
                    {
                        IField targetField = targetFC.Fields.get_Field(targetIdx);
                        if (targetField.Type == esriFieldType.esriFieldTypeGeometry)
                        {
                            featureBuffer.Shape = sourceFeature.Shape;
                        }
                        else if (targetField.Editable && targetField.Type != esriFieldType.esriFieldTypeOID)
                        {
                            featureBuffer.set_Value(targetIdx, sourceFeature.get_Value(i));
                        }
                    }
                }
                insertCursor.InsertFeature(featureBuffer);
                sourceFeature = sourceCursor.NextFeature();
                count++;
            }
            insertCursor.Flush();

            System.Runtime.InteropServices.Marshal.ReleaseComObject(sourceCursor);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(insertCursor);
        }

        private static IFields CloneFields(IFields sourceFields)
        {
            // 简单克隆逻辑，在导出要素类时保留原有字段定义
            return (IFields)((IClone)sourceFields).Clone();
        }

        /// <summary>
        /// 【点位离散化】：解决地图上多个非遗点地理坐标完全重合的问题
        /// 逻辑：将位于同一个坐标点上的要素，按圆形环状均匀展开，防止视觉上的点位遮挡
        /// </summary>
        /// <param name="featureClass">待处理的点要素类</param>
        /// <param name="offsetDistanceDegree">偏移距离（经纬度单位，0.0008 约为 80 米）</param>
        public static string DisplaceDuplicatePoints(IFeatureClass featureClass, double offsetDistanceDegree = 0.0008)
        {
            try
            {
                if (featureClass == null) return "要素类为空";
                if (featureClass.ShapeType != esriGeometryType.esriGeometryPoint) return "仅支持点要素类";

                // 1. 扫描所有位置并将要素分组
                Dictionary<string, List<int>> locationMap = new Dictionary<string, List<int>>();
                IFeatureCursor cursor = featureClass.Search(null, false);
                IFeature feature;

                while ((feature = cursor.NextFeature()) != null)
                {
                    IPoint pt = feature.Shape as IPoint;
                    if (pt == null || pt.IsEmpty) continue;

                    // 使用6位小数精度作为判定重叠的标准
                    string key = $"{pt.X:F6},{pt.Y:F6}";
                    if (!locationMap.ContainsKey(key))
                        locationMap[key] = new List<int>();

                    locationMap[key].Add(feature.OID);
                }
                System.Runtime.InteropServices.Marshal.ReleaseComObject(cursor);

                // 2. 识别需要处理的重叠组
                var duplicateGroups = locationMap.Values.Where(ids => ids.Count > 1).ToList();
                if (duplicateGroups.Count == 0) return "未发现重叠点,无需处理。";

                int processedCount = 0;
                // 3. 执行偏移计算与位置更新
                foreach (var ids in duplicateGroups)
                {
                    // 设置算法逻辑：保留第一个点作为簇中心，其余点按极坐标系均匀散落在圆周上
                    for (int i = 1; i < ids.Count; i++)
                    {
                        IFeature featToMove = featureClass.GetFeature(ids[i]);
                        IPoint pt = featToMove.ShapeCopy as IPoint;

                        // 计算环绕角度
                        double angle = (2 * Math.PI / (ids.Count - 1)) * (i - 1);
                        pt.X += offsetDistanceDegree * Math.Cos(angle);
                        pt.Y += offsetDistanceDegree * Math.Sin(angle);

                        featToMove.Shape = pt;
                        featToMove.Store();
                        processedCount++;
                    }
                }

                return $"离散化完成！处理了 {duplicateGroups.Count} 处重叠, 移动了 {processedCount} 个要素。";
            }
            catch (Exception ex)
            {
                return "离散化失败: " + ex.Message;
            }
        }
        /// <summary>
        /// 获取图层中满足条件的要素数量
        /// </summary>
        /// <param name="featureClass">目标要素类</param>
        /// <param name="whereClause">查询条件 (例如 "City = '济南市'")，为空则统计所有</param>
        /// <returns>要素数量</returns>
        public static int GetFeatureCount(IFeatureClass featureClass, string whereClause = "")
        {
            if (featureClass == null) return 0;
            try
            {
                IQueryFilter queryFilter = new QueryFilterClass();
                queryFilter.WhereClause = whereClause;
                return featureClass.FeatureCount(queryFilter);
            }
            catch
            {
                return 0;
            }
        }
        /// <summary>
        /// 将几何体导出为 Shapefile (用于缓冲区导出)
        /// </summary>
        public static void ExportGeometryToShapefile(List<IGeometry> geometries, string filePath)
        {
            if (geometries == null || geometries.Count == 0) throw new Exception("没有几何体可导出");

            // 1. 创建 Shapefile
            string folder = System.IO.Path.GetDirectoryName(filePath);
            string name = System.IO.Path.GetFileName(filePath);

            IWorkspaceFactory workspaceFactory = new ShapefileWorkspaceFactoryClass();
            IFeatureWorkspace featureWorkspace = (IFeatureWorkspace)workspaceFactory.OpenFromFile(folder, 0);

            // 定义字段
            IFields fields = new FieldsClass();
            IFieldsEdit fieldsEdit = (IFieldsEdit)fields;

            // OID 字段
            IField fieldOID = new FieldClass();
            IFieldEdit fieldEditOID = (IFieldEdit)fieldOID;
            fieldEditOID.Name_2 = "OID";
            fieldEditOID.Type_2 = esriFieldType.esriFieldTypeOID;
            fieldsEdit.AddField(fieldOID);

            // Shape 字段
            IField fieldShape = new FieldClass();
            IFieldEdit fieldEditShape = (IFieldEdit)fieldShape;
            fieldEditShape.Name_2 = "Shape";
            fieldEditShape.Type_2 = esriFieldType.esriFieldTypeGeometry;

            IGeometryDef geometryDef = new GeometryDefClass();
            IGeometryDefEdit geometryDefEdit = (IGeometryDefEdit)geometryDef;
            geometryDefEdit.GeometryType_2 = geometries[0].GeometryType;
            geometryDefEdit.SpatialReference_2 = geometries[0].SpatialReference;
            fieldEditShape.GeometryDef_2 = geometryDef;
            fieldsEdit.AddField(fieldShape);

            // 创建要素类
            IFeatureClass featureClass = featureWorkspace.CreateFeatureClass(name, fields, null, null, esriFeatureType.esriFTSimple, "Shape", "");

            // 2. 插入数据
            IFeatureCursor cursor = featureClass.Insert(true);
            foreach (var geo in geometries)
            {
                IFeatureBuffer buffer = featureClass.CreateFeatureBuffer();
                buffer.Shape = geo;
                cursor.InsertFeature(buffer);
            }
            cursor.Flush();
        }

        /// <summary>
        /// 导出选中的要素到 Shapefile
        /// </summary>
        public static void ExportSelectionToShapefile(IFeatureLayer featureLayer, string filePath)
        {
            IFeatureSelection featureSelection = featureLayer as IFeatureSelection;
            ISelectionSet selectionSet = featureSelection.SelectionSet;

            if (selectionSet.Count == 0) throw new Exception("没有选中任何要素");

            // 1. 获取源字段定义
            IFeatureClass sourceClass = featureLayer.FeatureClass;
            IFields sourceFields = sourceClass.Fields;

            // 2. 创建目标 Shapefile
            string folder = System.IO.Path.GetDirectoryName(filePath);
            string name = System.IO.Path.GetFileName(filePath);

            IWorkspaceFactory workspaceFactory = new ShapefileWorkspaceFactoryClass();
            IFeatureWorkspace featureWorkspace = (IFeatureWorkspace)workspaceFactory.OpenFromFile(folder, 0);

            // 克隆字段 (Shapefile 有字段名长度限制，可能需要截断，暂简化处理)
            // 为简单起见，我们重新定义核心字段，或者使用 IFeatureDataConverter 导出
            // 这里使用更底层的 IFeatureDataConverter 导出选集会更稳健

            ExportByExporter(featureLayer, filePath);
        }

        private static void ExportByExporter(IFeatureLayer sourceLayer, string targetPath)
        {
            // 使用 Geoprocessor 或 FeatureDataConverter 导出选集
            // 这里为了简化依赖，手动循环插入

            string folder = System.IO.Path.GetDirectoryName(targetPath);
            string name = System.IO.Path.GetFileName(targetPath);
            IWorkspaceFactory wf = new ShapefileWorkspaceFactoryClass();
            IFeatureWorkspace fw = (IFeatureWorkspace)wf.OpenFromFile(folder, 0);

            // 创建要素类 (使用源字段，注意Shapefile限制)
            // 简化：仅保留 Shape 字段以避免复杂性，或者仅导出几何
            // 实际上，为了稳健，我们通过 GeoProcessor 导出可能更好，但 AE 环境下 GP 较慢
            // 还是写循环吧

            IFeatureClass sourceInfo = sourceLayer.FeatureClass;
            // 简化：新建只有几何的 Shapefile，或者克隆字段
            // Clone 字段逻辑需要处理别名和类型映射，较繁琐。
            // 这种情况下，使用 IFeatureDataConverter 是标准做法。

            IDataset sourceDataset = sourceLayer.FeatureClass as IDataset;
            IDatasetName sourceDatasetName = sourceDataset.FullName as IDatasetName;

            IWorkspaceName targetWorkspaceName = new WorkspaceNameClass();
            targetWorkspaceName.WorkspaceFactoryProgID = "esriDataSourcesFile.ShapefileWorkspaceFactory";
            targetWorkspaceName.PathName = folder;

            IFeatureClassName targetFeatureClassName = new FeatureClassNameClass();
            IDatasetName targetDatasetName = (IDatasetName)targetFeatureClassName;
            targetDatasetName.Name = name;
            targetDatasetName.WorkspaceName = targetWorkspaceName;

            IFeatureDataConverter converter = new FeatureDataConverterClass();

            // 仅导出选中要素
            // 但是我们需要SelectionSet。Converter通常作用于FeatureClass。
            // 这里我们用 EnumFeatureGeometry 的方式手动写入最可靠

            // 回退到手动写入：
            ManualExportSelection(sourceLayer, fw, name);
        }

        private static void ManualExportSelection(IFeatureLayer layer, IFeatureWorkspace fw, string name)
        {
            IFeatureClass srcClass = layer.FeatureClass;
            // 简化：仅创建 Shape 字段
            IFields fields = new FieldsClass();
            IFieldsEdit fieldsEdit = (IFieldsEdit)fields;

            // OID
            IField oidField = new FieldClass();
            ((IFieldEdit)oidField).Name_2 = "OID";
            ((IFieldEdit)oidField).Type_2 = esriFieldType.esriFieldTypeOID;
            fieldsEdit.AddField(oidField);

            // Shape
            IField shapeField = new FieldClass();
            ((IFieldEdit)shapeField).Name_2 = "Shape";
            ((IFieldEdit)shapeField).Type_2 = esriFieldType.esriFieldTypeGeometry;
            ((IFieldEdit)shapeField).GeometryDef_2 = srcClass.Fields.get_Field(srcClass.FindField(srcClass.ShapeFieldName)).GeometryDef;
            fieldsEdit.AddField(shapeField);

            // 创建
            IFeatureClass tgtClass = fw.CreateFeatureClass(name, fields, null, null, esriFeatureType.esriFTSimple, "Shape", "");

            // 插入选集
            IFeatureSelection fSel = (IFeatureSelection)layer;
            IEnumIDs enumIDs = fSel.SelectionSet.IDs;
            int id = enumIDs.Next();

            IFeatureCursor insertCursor = tgtClass.Insert(true);

            while (id != -1)
            {
                IFeature srcFeat = srcClass.GetFeature(id);
                IFeatureBuffer buf = tgtClass.CreateFeatureBuffer();
                buf.Shape = srcFeat.ShapeCopy;
                insertCursor.InsertFeature(buf);
                id = enumIDs.Next();
            }
            insertCursor.Flush();
        }
    }
}
