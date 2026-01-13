using System;
using System.Data;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;

namespace WindowsFormsMap1
{
    /// <summary>
    /// WebView2 交互桥接类
    /// 允许 JS 直接调用 C# 方法操作数据库
    /// </summary>
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class WebBridge
    {
        // 1. 获取基础数据 (供 JS 初始化调用)
        public string GetAllData()
        {
            try
            {
                // 获取统计数据
                var cityStats = new List<CityStat>();
                DataTable dtCity = DBHelper.ExecuteQuery("SELECT City, COUNT(*) as Cnt FROM ICH_Items GROUP BY City");
                foreach (DataRow dr in dtCity.Rows)
                {
                    cityStats.Add(new CityStat { name = dr["City"].ToString(), value = Convert.ToInt32(dr["Cnt"]) });
                }

                var catStats = new List<CategoryStat>();
                DataTable dtCat = DBHelper.ExecuteQuery("SELECT Category, COUNT(*) as Cnt FROM ICH_Items GROUP BY Category");
                foreach (DataRow dr in dtCat.Rows)
                {
                    catStats.Add(new CategoryStat { name = dr["Category"].ToString(), count = Convert.ToInt32(dr["Cnt"]) });
                }

                // 获取点位数据 (限制 1000 条以免前端卡顿)
                var points = new List<ProjectPoint>();
                DataTable dtPoints = DBHelper.ExecuteQuery("SELECT TOP 1000 Name, Category, City, Latitude, Longitude, Batch FROM ICH_Items");
                foreach (DataRow dr in dtPoints.Rows)
                {
                    points.Add(new ProjectPoint
                    {
                        name = dr["Name"].ToString(),
                        category = dr["Category"].ToString(),
                        city = dr["City"].ToString(),
                        y = Convert.ToDouble(dr["Latitude"]),
                        x = Convert.ToDouble(dr["Longitude"]),
                        batch = dr["Batch"] != DBNull.Value ? Convert.ToInt32(dr["Batch"]) : 0
                    });
                }

                // [Debug] Show data count
                // MessageBox.Show($"Debug: Loaded {points.Count} items from DB.", "Data Check");

                WebData data = new WebData();
                data.projectInfo.lastUpdated = DateTime.Now.ToString("yyyy-MM-dd");
                data.projectInfo.totalItems = Convert.ToInt32(DBHelper.ExecuteScalar("SELECT COUNT(*) FROM ICH_Items"));
                data.statsByCity = cityStats;
                data.categories = catStats;
                data.points = points;

                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                string result = serializer.Serialize(data);
                // System.IO.File.WriteAllText("c:\\debug_data.json", result); // Optional file debug
                return result;
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebBridge Error: " + ex.Message);
                return "{ \"error\": \"" + ex.Message + "\" }";
            }
        }

        // 2. 点赞互动
        public bool AddLike(string itemName)
        {
            try
            {
                // 简单起见，我们暂时用 Name 来关联，正规应使用 ID
                // 记录到 User_Actions 表
                string sql = @"INSERT INTO User_Actions (ItemID, ActionType, ActionValue, ActionTime) 
                               SELECT ID, 'LIKE', '1', GETDATE() FROM ICH_Items WHERE Name = @Name";

                System.Data.SqlClient.SqlParameter[] p = {
                    new System.Data.SqlClient.SqlParameter("@Name", itemName)
                };

                int rows = DBHelper.ExecuteNonQuery(sql, p);
                return rows > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // 3. 发表评论
        public bool AddComment(string itemName, string comment)
        {
            try
            {
                string sql = @"INSERT INTO User_Actions (ItemID, ActionType, ActionValue, ActionTime) 
                               SELECT ID, 'COMMENT', @Comment, GETDATE() FROM ICH_Items WHERE Name = @Name";

                System.Data.SqlClient.SqlParameter[] p = {
                    new System.Data.SqlClient.SqlParameter("@Name", itemName),
                    new System.Data.SqlClient.SqlParameter("@Comment", comment)
                };

                int rows = DBHelper.ExecuteNonQuery(sql, p);
                return rows > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // 4. 获取评论列表
        public string GetComments(string itemName)
        {
            try
            {
                string sql = @"SELECT ActionValue, ActionTime FROM User_Actions 
                               INNER JOIN ICH_Items ON User_Actions.ItemID = ICH_Items.ID
                               WHERE ICH_Items.Name = @Name AND ActionType = 'COMMENT'
                               ORDER BY ActionTime DESC"; // 最新评论在前

                System.Data.SqlClient.SqlParameter[] p = {
                    new System.Data.SqlClient.SqlParameter("@Name", itemName)
                };

                DataTable dt = DBHelper.ExecuteQuery(sql, p);
                var comments = new List<object>();
                foreach (DataRow dr in dt.Rows)
                {
                    comments.Add(new
                    {
                        text = dr["ActionValue"].ToString(),
                        date = Convert.ToDateTime(dr["ActionTime"]).ToString("MM-dd HH:mm")
                    });
                }

                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                return serializer.Serialize(comments);
            }
            catch
            {
                return "[]";
            }
        }

        // --- 数据模型类 ---
        public class ProjectPoint
        {
            public string name { get; set; }
            public string category { get; set; }
            public string city { get; set; }
            public double x { get; set; } // Longitude
            public double y { get; set; } // Latitude
            public int batch { get; set; } // 公布批次 (1-5)
        }

        public class CityStat
        {
            public string name { get; set; }
            public int value { get; set; }
        }

        public class CategoryStat
        {
            public string name { get; set; }
            public int count { get; set; }
        }

        public class WebData
        {
            public ProjectInfo projectInfo { get; set; } = new ProjectInfo();
            public List<CityStat> statsByCity { get; set; }
            public List<CategoryStat> categories { get; set; }
            public List<ProjectPoint> points { get; set; }
        }

        public class ProjectInfo
        {
            public string title { get; set; } = "山东省非物质文化遗产大数据概览";
            public int totalItems { get; set; }
            public string lastUpdated { get; set; }
        }
    }
}
