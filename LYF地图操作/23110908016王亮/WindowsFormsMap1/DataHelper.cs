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
        /// 数据瘦身：从全国数据中提取山东省数据并保存到 GDB
        /// </summary>
        /// <param name="sourceShpPath">原始 Shapefile 完整路径</param>
        /// <param name="targetGdbDir">目标 GDB 所在目录</param>
        /// <param name="targetGdbName">目标 GDB 名称 (如 ShandongICH.gdb)</param>
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

                // 3. 构建过滤条件 (提取山东省)
                IQueryFilter queryFilter = new QueryFilterClass();
                queryFilter.WhereClause = "省 = '山东省'";

                // 4. 定位目标要素类名称
                string className = System.IO.Path.GetFileNameWithoutExtension(sourceShpPath) + "_SD";

                // 如果已存在，则删除 (简单重置逻辑)
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
            // 创建目标要素类 (结构同源)
            IFields targetFields = CloneFields(sourceFC.Fields);

            // [新增] 检查并增加“类别”字段
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
            // 简单克隆逻辑，实际生产中需更严谨处理 GeometryDef
            return (IFields)((IClone)sourceFields).Clone();
        }

        /// <summary>
        /// 离散化处理：将重叠在同一点的要素稍微散开，呈环状分布
        /// </summary>
        /// <param name="featureClass">要处理的要素类</param>
        /// <param name="offsetDistanceDegree">偏移距离(经纬度单位,0.0005约为50米)</param>
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
                // 3. 执行偏移处理
                foreach (var ids in duplicateGroups)
                {
                    // 每一个组，让第一个保持不动(中心)，其余环绕
                    for (int i = 1; i < ids.Count; i++)
                    {
                        IFeature featToMove = featureClass.GetFeature(ids[i]);
                        IPoint pt = featToMove.ShapeCopy as IPoint;

                        // 计算环绕位置: 均匀分布在圆周上
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
    }
}
