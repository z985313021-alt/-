using System;
using System.Data;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;

namespace WindowsFormsMap1
{
    /// <summary>
    /// 【Web 指挥桥接类】：负责 C# 宿主程序与 WebView2 网页前端的双向通信
    /// 逻辑：JS 通过 window.chrome.webview.hostObjects.bridge 调用此处的方法，实现网页端对本地数据库的访问
    /// </summary>
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class WebBridge
    {
        // 【数据初始化】：由网页端加载时首选调用，获取全省非遗项目的统计指标与空间坐标
        public string GetAllData()
        {
            try
            {
                // 1. 获取地市级统计数据（用于绘制主屏柱状图/热力图）
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

                // 3. 提取非遗项目点位（限制 1000 条以确保 Web GL 渲染流畅，避免前端内存溢出）
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

                // 4. 封装并序列化为标准 JSON 字符串
                WebData data = new WebData();
                data.projectInfo.lastUpdated = DateTime.Now.ToString("yyyy-MM-dd");
                data.projectInfo.totalItems = Convert.ToInt32(DBHelper.ExecuteScalar("SELECT COUNT(*) FROM ICH_Items"));
                data.statsByCity = cityStats;
                data.categories = catStats;
                data.points = points;

                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                string result = serializer.Serialize(data);
                return result;
            }
            catch (Exception ex)
            {
                MessageBox.Show("数据桥接解析执行错误: " + ex.Message);
                return "{ \"error\": \"" + ex.Message + "\" }";
            }
        }

        // 【交互持久化：点赞】：接收前端传回的项目名称，并在数据库 User_Actions 中记录点赞行为
        public bool AddLike(string itemName)
        {
            try
            {
                // SQL 逻辑：将动作类型标记为 'LIKE'
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

        // 【交互持久化：评论】：接收前端传回的项目名称与评论内容，记录到数据库 User_Actions 表
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

        // 【实时交互：获取评论】：从数据库提取该非遗项目的所有历史评价，按时间倒序排列
        public string GetComments(string itemName)
        {
            try
            {
                string sql = @"SELECT ActionValue, ActionTime FROM User_Actions 
                               INNER JOIN ICH_Items ON User_Actions.ItemID = ICH_Items.ID
                               WHERE ICH_Items.Name = @Name AND ActionType = 'COMMENT'
                               ORDER BY ActionTime DESC";

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

        // 【数据传输：路网数据】：直接从本地文件读取路网数据，避免 WebView2 的 ExecuteScriptAsync 方法对大数据量的限制
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
                    string devPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, @"..\..\VisualWeb", "data", "roads.json"));
                    if (System.IO.File.Exists(devPath)) path = devPath;
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

        // --- 数据模型定义：用于 JSON 序列化，方便 C# 与 JS 之间的数据交换 ---
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

        // 【路线管理：保存】：保存由 JS 规划出的推荐游览路线（点位坐标串及名称）
        public bool SaveRoute(string routeName, double startLng, double startLat,
                              double endLng, double endLat, string ichItemsJson, string description)
        {
            try
            {
                // 1. 插入路线基本信息
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

                if (result == null) return false;

                int routeId = Convert.ToInt32(result);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // 【路线管理：读取】：获取数据库中存储的所有历史路线摘要
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
            catch
            {
                return "[]";
            }
        }

        // 【路线管理：详情】：根据路线 ID 获取单条路线的详细信息
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
