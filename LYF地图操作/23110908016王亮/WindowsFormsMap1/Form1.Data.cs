// [Agent (通用辅助)] Modified: 中文化注释与架构梳理
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geodatabase;

namespace WindowsFormsMap1
{
    public partial class Form1
    {
        // 成员 D：数据中台 (Data Fabric)

        public void InitDataModule()
        {
            // [Member D] 数据导入功能已移除 (任务完成)
        }

        /// <summary>
        /// [Member D] 增强型图层定位助手：支持双控制器搜索、解耦可见性并扩展关键词
        /// </summary>
        private IFeatureLayer FindHeritageLayer()
        {
            // 优先级 1：检查专业版地图 (axMapControl2)
            IFeatureLayer layer = SearchLayerInControl(axMapControl2);
            if (layer != null) return layer;

            // 优先级 2：检查演示版地图 (axMapControlVisual)
            layer = SearchLayerInControl(axMapControlVisual);
            if (layer != null) return layer;

            return null;
        }

        private IFeatureLayer SearchLayerInControl(ESRI.ArcGIS.Controls.AxMapControl mapControl)
        {
            if (mapControl == null || mapControl.LayerCount == 0) return null;

            // 关键词库：按匹配度降序
            string[] keys = { "非遗", "名录", "项目", "ICH", "SD", "山东", "点", "数据" };

            for (int i = 0; i < mapControl.LayerCount; i++)
            {
                ILayer layer = mapControl.get_Layer(i);
                if (layer is IFeatureLayer)
                {
                    string name = layer.Name.ToUpper();
                    foreach (string key in keys)
                    {
                        if (name.Contains(key.ToUpper()))
                            return layer as IFeatureLayer;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// [Member D] 内部辅助：将年份过滤逻辑应用到指定的地图控件
        /// </summary>
        private void ApplyYearFilterToControl(ESRI.ArcGIS.Controls.AxMapControl mapControl, int year)
        {
            if (mapControl == null || mapControl.LayerCount == 0) return;

            try
            {
                IFeatureLayer heritageLayer = null;
                for (int i = 0; i < mapControl.LayerCount; i++)
                {
                    ILayer layer = mapControl.get_Layer(i);
                    if (layer is IFeatureLayer)
                    {
                        string ln = layer.Name;
                        if (ln.Contains("非遗") || ln.Contains("名录") || ln.Contains("项目") || ln.Contains("ICH"))
                        {
                            heritageLayer = layer as IFeatureLayer;
                            break;
                        }
                    }
                }

                if (heritageLayer != null)
                {
                    IFields fields = heritageLayer.FeatureClass.Fields;
                    string timeField = "";
                    string batchField = "";

                    // [Agent Modified] 动态查找字段，提升兼容性
                    for (int j = 0; j < fields.FieldCount; j++)
                    {
                        string fn = fields.get_Field(j).Name;
                        if (fn.Contains("公布时间") || fn.ToUpper().Contains("YEAR") || fn.ToUpper().Contains("TIME")) timeField = fn;
                        if (fn.Contains("公布批次") || fn.ToUpper().Contains("BATCH") || fn.ToUpper().Contains("PC")) batchField = fn;
                    }

                    if (!string.IsNullOrEmpty(timeField) || !string.IsNullOrEmpty(batchField))
                    {
                        List<string> conditions = new List<string>();
                        if (!string.IsNullOrEmpty(timeField))
                        {
                            conditions.Add($"({timeField} >= 1900 AND {timeField} <= {year})");
                        }

                        if (!string.IsNullOrEmpty(batchField))
                        {
                            int maxBatch = 0;
                            if (year >= 2006) maxBatch = 1;
                            if (year >= 2008) maxBatch = 2;
                            if (year >= 2011) maxBatch = 3;
                            if (year >= 2014) maxBatch = 4;
                            if (year >= 2021) maxBatch = 5;
                            conditions.Add($"({batchField} >= 1 AND {batchField} <= {maxBatch} AND {batchField} < 20)");
                        }

                        string sqlFilter = string.Join(" OR ", conditions);
                        IFeatureLayerDefinition layerDef = heritageLayer as IFeatureLayerDefinition;
                        if (layerDef != null) layerDef.DefinitionExpression = sqlFilter;

                        // [Optimization] 仅当控件可见时才刷新，避免后台无意义的渲染消耗
                        if (mapControl.Visible && mapControl.Parent != null && mapControl.Parent.Visible)
                        {
                            mapControl.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// [API] 获取指定城市的非遗项目数量 (基于真实数据字段查询)
        /// </summary>
        /// <param name="cityName">城市名称</param>
        /// <param name="year">当前滑块年份</param>
        public int GetCountByCity(string cityName, int year)
        {
            try
            {
                // 1. 定位目标图层
                IFeatureLayer targetLayer = FindHeritageLayer();
                if (targetLayer == null) return 0;

                IFields fields = targetLayer.FeatureClass.Fields;

                // 2. 匹配地市字段 (权重排序：确信度高的排前面)
                string realCityField = "";
                string[] cityKeys = { "地市", "所属地区", "行政区", "县市区", "DS", "CITY", "QX", "COUNTY", "市", "NAME", "Name" };
                for (int i = 0; i < fields.FieldCount; i++)
                {
                    string fName = fields.get_Field(i).Name.ToUpper();
                    foreach (string k in cityKeys)
                    {
                        if (fName.Contains(k.ToUpper()))
                        {
                            realCityField = fields.get_Field(i).Name;
                            // 如果找到了包含“地市”或“所属地区”的字段，优先确认
                            if (fName.Contains("地市") || fName.Contains("地区")) break;
                        }
                    }
                    if (!string.IsNullOrEmpty(realCityField) && (realCityField.Contains("地市") || realCityField.Contains("地区"))) break;
                }

                // 兜底：如果没找到地市字段，但有 NAME 且不是地市确信度高的，则保留最后一次匹配

                // 3. 匹配时间字段 (包含“公布时间”和“公布批次”)
                string realTimeField = "";
                bool isNumeric = false;
                string[] timeKeys = { "公布时间", "时间", "YEAR", "TIME", "批次", "BATCH", "SJ" };
                for (int i = 0; i < fields.FieldCount; i++)
                {
                    string fName = fields.get_Field(i).Name.ToUpper();
                    foreach (string k in timeKeys)
                    {
                        if (fName.Contains(k.ToUpper()))
                        {
                            IField f = fields.get_Field(i);
                            realTimeField = f.Name;
                            isNumeric = (f.Type != esriFieldType.esriFieldTypeString && f.Type != esriFieldType.esriFieldTypeDate);
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(realTimeField)) break;
                }

                // 4. 构建 SQL
                string baseWhere = "";
                string shortName = cityName.Replace("市", "");
                if (!string.IsNullOrEmpty(realCityField))
                {
                    baseWhere = $"({realCityField} = '{cityName}' OR {realCityField} LIKE '%{shortName}%')";
                }

                // 如果没有时间字段，只能返回城市总数 (不编造数据)
                if (string.IsNullOrEmpty(realTimeField))
                {
                    return DataHelper.GetFeatureCount(targetLayer.FeatureClass, baseWhere);
                }

                // 映射滑块年份到国家级批次 (1-5)
                int batch = 0;
                if (year >= 2021) batch = 5;
                else if (year >= 2014) batch = 4;
                else if (year >= 2011) batch = 3;
                else if (year >= 2008) batch = 2;
                else if (year >= 2006) batch = 1;

                string timeClause = "";
                if (isNumeric)
                {
                    timeClause = $"(({realTimeField} >= 1900 AND {realTimeField} <= {year}) OR ({realTimeField} > 0 AND {realTimeField} <= {batch} AND {realTimeField} < 20))";
                }
                else
                {
                    timeClause = $"({realTimeField} LIKE '%{year}%' OR {realTimeField} LIKE '%{batch}%')";
                }

                string finalWhere = string.IsNullOrEmpty(baseWhere) ? timeClause : $"{baseWhere} AND {timeClause}";
                return DataHelper.GetFeatureCount(targetLayer.FeatureClass, finalWhere);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// [API] 获取指定年份的非遗项目类别统计
        /// </summary>
        /// <param name="year">当前滑块年份</param>
        /// <returns>Key:类别名称, Value:数量</returns>
        public Dictionary<string, int> GetCategoryStats(int year)
        {
            Dictionary<string, int> stats = new Dictionary<string, int>();
            try
            {
                // 1. 定位目标图层
                IFeatureLayer targetLayer = FindHeritageLayer();
                if (targetLayer == null) return stats;

                IFields fields = targetLayer.FeatureClass.Fields;

                // 2. 匹配类别字段
                string categoryField = "";
                string[] catKeys = { "类别", "Category", "Type", "LB", "XM_LB", "PROJECT_TYPE", "项目类型" };
                for (int i = 0; i < fields.FieldCount; i++)
                {
                    string fName = fields.get_Field(i).Name.ToUpper();
                    foreach (string k in catKeys)
                    {
                        if (fName.Contains(k.ToUpper()))
                        {
                            categoryField = fields.get_Field(i).Name;
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(categoryField)) break;
                }

                if (string.IsNullOrEmpty(categoryField)) return stats;

                // 3. 匹配时间字段 (复用逻辑)
                string realTimeField = "";
                bool isNumeric = false;
                string[] timeKeys = { "公布时间", "时间", "YEAR", "TIME", "批次", "BATCH" };
                for (int i = 0; i < fields.FieldCount; i++)
                {
                    string fName = fields.get_Field(i).Name.ToUpper();
                    foreach (string k in timeKeys)
                    {
                        if (fName.Contains(k.ToUpper()))
                        {
                            IField f = fields.get_Field(i);
                            realTimeField = f.Name;
                            isNumeric = (f.Type != esriFieldType.esriFieldTypeString && f.Type != esriFieldType.esriFieldTypeDate);
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(realTimeField)) break;
                }

                // 4. 构建查询条件
                string timeClause = "";
                if (!string.IsNullOrEmpty(realTimeField))
                {
                    int batch = 0;
                    if (year >= 2021) batch = 5;
                    else if (year >= 2014) batch = 4;
                    else if (year >= 2011) batch = 3;
                    else if (year >= 2008) batch = 2;
                    else if (year >= 2006) batch = 1;

                    if (isNumeric)
                    {
                        timeClause = $"(({realTimeField} >= 1900 AND {realTimeField} <= {year}) OR ({realTimeField} > 0 AND {realTimeField} <= {batch} AND {realTimeField} < 20))";
                    }
                    else
                    {
                        timeClause = $"({realTimeField} LIKE '%{year}%' OR {realTimeField} LIKE '%{batch}%')";
                    }
                }

                // 5. 执行查询并统计
                IQueryFilter queryFilter = new QueryFilterClass();
                if (!string.IsNullOrEmpty(timeClause))
                {
                    queryFilter.WhereClause = timeClause;
                }

                IFeatureCursor cursor = targetLayer.Search(queryFilter, true);
                IFeature feature = cursor.NextFeature();

                int catIdx = fields.FindField(categoryField);
                if (catIdx == -1) return stats;

                while (feature != null)
                {
                    object val = feature.get_Value(catIdx);
                    string catName = (val != null && val != DBNull.Value) ? val.ToString().Trim() : "其他";
                    if (string.IsNullOrEmpty(catName)) catName = "其他";

                    if (stats.ContainsKey(catName)) stats[catName]++;
                    else stats[catName] = 1;

                    feature = cursor.NextFeature();
                }
                System.Runtime.InteropServices.Marshal.ReleaseComObject(cursor);
            }
            catch (Exception)
            {
                // 静默忽略异常，保证 UI 不会因为数据查询错误而崩溃
            }
            return stats;
        }
    }
}
