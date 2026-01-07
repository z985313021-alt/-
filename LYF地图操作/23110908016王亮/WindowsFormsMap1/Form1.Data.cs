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
                // 1. 定位目标波段/图层 (优先找非遗名录点位)
                IFeatureLayer targetLayer = null;
                for (int i = 0; i < axMapControl2.LayerCount; i++)
                {
                    ILayer layer = axMapControl2.get_Layer(i);
                    if (layer is IFeatureLayer && layer.Visible)
                    {
                        string n = layer.Name;
                        if (n.Contains("名录") || n.Contains("项目") || n.Contains("非遗") || n.Contains("ICH"))
                        {
                            targetLayer = layer as IFeatureLayer;
                            break;
                        }
                    }
                }
                if (targetLayer == null && axMapControl2.LayerCount > 0)
                {
                    ILayer l = axMapControl2.get_Layer(0);
                    if (l is IFeatureLayer) targetLayer = l as IFeatureLayer;
                }
                if (targetLayer == null) return 0;

                IFields fields = targetLayer.FeatureClass.Fields;

                // 2. 匹配城市字段
                string realCityField = "";
                string[] cityKeys = { "市", "City", "Name", "NAME", "地市", "所属地区" };
                for (int i = 0; i < fields.FieldCount; i++)
                {
                    string fName = fields.get_Field(i).Name;
                    foreach (string k in cityKeys) if (fName.Equals(k, StringComparison.OrdinalIgnoreCase)) { realCityField = fName; break; }
                    if (!string.IsNullOrEmpty(realCityField)) break;
                }

                // 3. 匹配时间字段 (重点：公布时间)
                string realTimeField = "";
                bool isNumeric = false;
                // 优先找 XML 中确认的 "公布时间"
                int tIdx = fields.FindField("公布时间");
                if (tIdx == -1) tIdx = fields.FindField("Time");
                if (tIdx == -1) tIdx = fields.FindField("Year");
                
                if (tIdx != -1)
                {
                    IField f = fields.get_Field(tIdx);
                    realTimeField = f.Name;
                    isNumeric = (f.Type != esriFieldType.esriFieldTypeString && f.Type != esriFieldType.esriFieldTypeDate);
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

                // 构建 [双模查询] 逻辑：
                // 同时支持字段存的是真实年份 (2006) 或 批次序号 (1)
                // 并剔除 0 或 无效数据
                string timeClause = "";
                if (isNumeric)
                {
                    // 逻辑：(字段是年份且符合) OR (字段是批次且符合)
                    timeClause = $"(({realTimeField} >= 1900 AND {realTimeField} <= {year}) OR ({realTimeField} > 0 AND {realTimeField} <= {batch} AND {realTimeField} < 20))";
                }
                else
                {
                    // 字符串模糊匹配 (如 "第一批" 或 "2006年")
                    timeClause = $"({realTimeField} LIKE '%{year}%' OR {realTimeField} LIKE '%{batch}%')";
                }

                string finalWhere = string.IsNullOrEmpty(baseWhere) ? timeClause : $"{baseWhere} AND {timeClause}";

                // 5. 执行查询
                return DataHelper.GetFeatureCount(targetLayer.FeatureClass, finalWhere);
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
