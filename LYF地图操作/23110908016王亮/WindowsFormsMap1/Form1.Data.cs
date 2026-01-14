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
        // 【数据中台模块】：负责为图表可视化系统（ECharts）提供底层 GIS 空间数据的查询与统计接口
        public void InitDataModule()
        {
            // [Member D] 此处可进行数据库初始化等操作
        }

        /// <summary>
        /// 【图层自动定位系统】：通过多级搜索机制，在当前地图中自动寻找并锁定“非遗”相关的核心图层
        /// 逻辑：优先检查活动地图，再检查后台控件，支持根据内置关键词库进行模糊匹配
        /// </summary>
        private IFeatureLayer FindHeritageLayer()
        {
            // 第一级：搜索 GIS 专业模式下的活动地图 (axMapControl2)
            IFeatureLayer layer = SearchLayerInControl(axMapControl2);
            if (layer != null) return layer;

            // 第二级：搜索可视化演示模式下的地图 (axMapControlVisual)
            layer = SearchLayerInControl(axMapControlVisual);
            if (layer != null) return layer;

            return null;
        }

        // 【图层特征扫描】：在指定的地图控件中遍历所有图层，识别包含地理实体的要素类图层
        private IFeatureLayer SearchLayerInControl(ESRI.ArcGIS.Controls.AxMapControl mapControl)
        {
            if (mapControl == null || mapControl.LayerCount == 0) return null;

            // 关键词库：用于识别非遗点位图层的标识符
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
        /// 【动态时间过滤】：根据时间轴滑块选定的年份，动态更新地图上展示的非遗点位
        /// 逻辑：自动识别图层中的“公布时间”或“批次”字段，构建 SQL 定义查询子句
        /// </summary>
        private void ApplyYearFilterToControl(ESRI.ArcGIS.Controls.AxMapControl mapControl, int year)
        {
            if (mapControl == null || mapControl.LayerCount == 0) return;

            try
            {
                // 1. 获取核心非遗图层
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
                    string timeField = "";   // 存储捕获到的时间字段名
                    string batchField = "";  // 存储捕获到的批次字段名

                    // 2. 智能匹配数据库字段（解决不同版本数据字段名不统一的问题）
                    for (int j = 0; j < fields.FieldCount; j++)
                    {
                        string fn = fields.get_Field(j).Name;
                        if (fn.Contains("公布时间") || fn.ToUpper().Contains("YEAR") || fn.ToUpper().Contains("TIME")) timeField = fn;
                        if (fn.Contains("公布批次") || fn.ToUpper().Contains("BATCH") || fn.ToUpper().Contains("PC")) batchField = fn;
                    }

                    if (!string.IsNullOrEmpty(timeField) || !string.IsNullOrEmpty(batchField))
                    {
                        List<string> conditions = new List<string>();
                        // 过滤规则 1：按具体年份过滤
                        if (!string.IsNullOrEmpty(timeField))
                        {
                            conditions.Add($"({timeField} >= 1900 AND {timeField} <= {year})");
                        }

                        // 过滤规则 2：按国家级批次对应年份过滤
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

                        // 执行 SQL 过滤（设置 DefinitionExpression）
                        string sqlFilter = string.Join(" OR ", conditions);
                        IFeatureLayerDefinition layerDef = heritageLayer as IFeatureLayerDefinition;
                        if (layerDef != null) layerDef.DefinitionExpression = sqlFilter;

                        // 局部刷新地理图层
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
        /// 【核心接口】：根据地市名称和当前年份获取非遗项目数量
        /// 逻辑：自动匹配字段名（地市/行政区等），并根据时间滑块位置进行过滤
        /// </summary>
        public int GetCountByCity(string cityName, int year)
        {
            try
            {
                // 1. 定位目标图层
                IFeatureLayer targetLayer = FindHeritageLayer();
                if (targetLayer == null) return 0;

                IFields fields = targetLayer.FeatureClass.Fields;

                // 2. 地市字段权重匹配（按确信度从高到低排列关键词）
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
                            // 优先锁定含有“地市”或“地区”的字段，因为它最符合业务定义
                            if (fName.Contains("地市") || fName.Contains("地区")) break;
                        }
                    }
                    if (!string.IsNullOrEmpty(realCityField) && (realCityField.Contains("地市") || realCityField.Contains("地区"))) break;
                }

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
        /// 【核心接口】：获取当前年份下的非遗项目类别比例（供饼图展示）
        /// 逻辑：遍历所有要素，按“类别”字段进行分组计数
        /// </summary>
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
