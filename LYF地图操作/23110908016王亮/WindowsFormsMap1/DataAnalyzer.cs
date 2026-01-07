using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace WindowsFormsMap1
{
    /// <summary>
    /// 数据分析工具类 - 用于检查非遗数据质量
    /// </summary>
    public class DataAnalyzer
    {
        /// <summary>
        /// 检查图层中是否有重复位置的要素
        /// </summary>
        public static string CheckDuplicateLocations(IFeatureLayer layer)
        {
            if (layer == null || layer.FeatureClass == null)
                return "图层无效";

            StringBuilder report = new StringBuilder();
            report.AppendLine("=== 非遗数据重复位置检查报告 ===\n");
            report.AppendLine($"图层名称: {layer.Name}");
            report.AppendLine($"总要素数: {layer.FeatureClass.FeatureCount(null)}\n");

            // 查找名称字段
            string nameField = FindNameField(layer.FeatureClass);
            if (string.IsNullOrEmpty(nameField))
            {
                report.AppendLine("警告: 未找到名称字段!");
                return report.ToString();
            }
            report.AppendLine($"使用字段: {nameField}\n");

            // 存储位置和对应的要素信息
            Dictionary<string, List<FeatureInfo>> locationDict = new Dictionary<string, List<FeatureInfo>>();

            // 遍历所有要素
            IFeatureCursor cursor = layer.FeatureClass.Search(null, false);
            IFeature feature;
            int totalCount = 0;

            while ((feature = cursor.NextFeature()) != null)
            {
                totalCount++;
                if (feature.Shape == null || feature.Shape.IsEmpty)
                    continue;

                IPoint pt = feature.Shape as IPoint;
                if (pt == null)
                    continue;

                // 创建位置键 (保留6位小数,约0.1米精度)
                string locationKey = $"{pt.X:F6},{pt.Y:F6}";

                // 获取名称
                int nameIdx = feature.Fields.FindField(nameField);
                string name = nameIdx >= 0 ? feature.get_Value(nameIdx)?.ToString() : "未知";

                // 添加到字典
                if (!locationDict.ContainsKey(locationKey))
                {
                    locationDict[locationKey] = new List<FeatureInfo>();
                }

                locationDict[locationKey].Add(new FeatureInfo
                {
                    OID = feature.OID,
                    Name = name,
                    X = pt.X,
                    Y = pt.Y
                });
            }
            System.Runtime.InteropServices.Marshal.ReleaseComObject(cursor);

            // 分析结果
            var duplicates = locationDict.Where(kvp => kvp.Value.Count > 1).ToList();

            report.AppendLine($"扫描完成: 共 {totalCount} 个要素");
            report.AppendLine($"唯一位置数: {locationDict.Count}");
            report.AppendLine($"重复位置数: {duplicates.Count}\n");

            if (duplicates.Count > 0)
            {
                report.AppendLine("【重复位置详情】");
                report.AppendLine("==================\n");

                int displayCount = 0;
                foreach (var dup in duplicates.OrderByDescending(d => d.Value.Count))
                {
                    displayCount++;
                    if (displayCount > 20) // 只显示前20个
                    {
                        report.AppendLine($"... 还有 {duplicates.Count - 20} 个重复位置未显示\n");
                        break;
                    }

                    var firstItem = dup.Value[0];
                    report.AppendLine($"位置 #{displayCount}: ({firstItem.X:F6}, {firstItem.Y:F6})");
                    report.AppendLine($"重复数量: {dup.Value.Count} 个要素");
                    report.AppendLine("包含项目:");
                    foreach (var item in dup.Value)
                    {
                        // 检查名称是否包含多个项目(用分号、逗号等分隔)
                        bool hasMultipleNames = item.Name.Contains(";") || 
                                               item.Name.Contains("、") || 
                                               item.Name.Contains(",");
                        string flag = hasMultipleNames ? " [名称字段包含多个项目!]" : "";
                        report.AppendLine($"  - OID:{item.OID} | {item.Name}{flag}");
                    }
                    report.AppendLine();
                }

                report.AppendLine("\n【问题分析】");
                report.AppendLine("==================");
                report.AppendLine("存在重复位置会导致以下问题:");
                report.AppendLine("1. 统计分析不准确(如按市统计非遗数量)");
                report.AppendLine("2. 热力图显示异常(同一位置权重过高)");
                report.AppendLine("3. 搜索时可能只显示第一个匹配项");
                report.AppendLine("\n【建议解决方案】");
                report.AppendLine("==================");
                report.AppendLine("方案1: 如果是数据录入错误,需要修正坐标");
                report.AppendLine("方案2: 如果确实在同一位置,考虑:");
                report.AppendLine("  - 合并为一条记录,名称用分号分隔");
                report.AppendLine("  - 或添加'项目数量'字段进行统计");
                report.AppendLine("  - 或使用聚合(Cluster)显示");
            }
            else
            {
                report.AppendLine("✓ 未发现重复位置,数据质量良好!");
            }

            return report.ToString();
        }

        /// <summary>
        /// 查找名称字段
        /// </summary>
        private static string FindNameField(IFeatureClass fc)
        {
            string[] candidates = { "名称", "项目名称", "Name", "TITLE", "NAME" };
            foreach (var field in candidates)
            {
                if (fc.Fields.FindField(field) != -1)
                    return field;
            }
            return null;
        }

        /// <summary>
        /// 要素信息类
        /// </summary>
        private class FeatureInfo
        {
            public int OID { get; set; }
            public string Name { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
        }
    }
}
