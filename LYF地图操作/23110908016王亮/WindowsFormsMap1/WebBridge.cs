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

        // 5. 获取大数据量的路网数据 (供 JS 直接拉取，避免 ExecuteScriptAsync 的大小限制)
        public string GetRoadsData()
        {
            try
            {
                // 统一路径逻辑
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string path = System.IO.Path.Combine(baseDir, "VisualWeb", "data", "roads.json");

                // 如果 bin 目录下没找到，尝试向上找源码目录 (开发环境下)
                if (!System.IO.File.Exists(path))
                {
                     path = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, @"..\..\VisualWeb", "data", "roads.json"));
                }

                if (System.IO.File.Exists(path))
                {
                    return System.IO.File.ReadAllText(path);
                }
                return "{\"error\": \"roads.json not found at " + path + "\"}";
            }
            catch (Exception ex)
            {
                return "{\"error\": \"" + ex.Message + "\"}";
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

        // [Member E] Added: 路线保存功能 - 保存路线起终点到数据库
        public bool SaveRoute(string routeName, double startLng, double startLat,
                              double endLng, double endLat, string ichItemsJson, string description)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[SaveRoute] 开始保存路线: {routeName}");
                System.Diagnostics.Debug.WriteLine($"[SaveRoute] 起点: ({startLng}, {startLat}), 终点: ({endLng}, {endLat})");

                // 插入路线记录到数据库
                string insertRoute = @"
                    INSERT INTO Saved_Routes 
                    (RouteName, StartLng, StartLat, EndLng, EndLat, Description)
                    VALUES (@name, @sLng, @sLat, @eLng, @eLat, @desc);
                    SELECT SCOPE_IDENTITY();";

                object result = DBHelper.ExecuteScalar(insertRoute,
                    new System.Data.SqlClient.SqlParameter("@name", routeName),
                    new System.Data.SqlClient.SqlParameter("@sLng", startLng),
                    new System.Data.SqlClient.SqlParameter("@sLat", startLat),
                    new System.Data.SqlClient.SqlParameter("@eLng", endLng),
                    new System.Data.SqlClient.SqlParameter("@eLat", endLat),
                    new System.Data.SqlClient.SqlParameter("@desc", description ?? ""));

                if (result == null)
                {
                    System.Diagnostics.Debug.WriteLine("[SaveRoute] ExecuteScalar返回null");
                    return false;
                }

                int routeId = Convert.ToInt32(result);
                System.Diagnostics.Debug.WriteLine($"[SaveRoute] 路线保存成功: ID={routeId}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SaveRoute] Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[SaveRoute] StackTrace: {ex.StackTrace}");
                return false;
            }
        }

        // [Member E] Added: 获取历史路线列表
        public string GetSavedRoutes()
        {
            try
            {
                string sql = @"
                    SELECT ID, RouteName, CreatedDate, Description,
                           CAST(StartLng AS VARCHAR) + ',' + CAST(StartLat AS VARCHAR) AS StartPoint,
                           CAST(EndLng AS VARCHAR) + ',' + CAST(EndLat AS VARCHAR) AS EndPoint
                    FROM Saved_Routes
                    ORDER BY CreatedDate DESC";

                return DBHelper.ExecuteJsonQuery(sql);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetSavedRoutes] Error: {ex.Message}");
                return "[]";
            }
        }

        // [Member E] Added: 获取特定路线的详细信息
        public string GetRouteDetail(int routeId)
        {
            try
            {
                string sql = $@"
                    SELECT ID, RouteName, StartLng, StartLat, EndLng, EndLat, Description, CreatedDate
                    FROM Saved_Routes
                    WHERE ID = {routeId}";

                string result = DBHelper.ExecuteJsonQuery(sql);
                
                // 返回第一条记录（去掉数组括号）
                if (result.StartsWith("[") && result.EndsWith("]") && result.Length > 2)
                {
                    return result.Substring(1, result.Length - 2);
                }
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetRouteDetail] Error: {ex.Message}");
                return "{}";
            }
        }
    }
}
