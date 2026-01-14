using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Windows.Forms;
using System.Text;

namespace WindowsFormsMap1
{
    /// <summary>
    /// 数据库操作助手 (SQL Server 版)
    /// 负责所有与 ICH_VisualDB 的交互
    /// </summary>
    public static class DBHelper
    {
        // 【连接字符串】：定义指向本地 SQL Server Express 实例的路径，默认使用 Windows 集成身份验证
        // 注意：Initial Catalog 指定了系统对应的演示数据库名称
        private static readonly string ConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=ICH_VisualDB;Integrated Security=True";

        // 【连接测试】：验证本地数据库服务是否已启动且库文件已正确挂载
        public static bool TestConnection()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    return true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("数据库连接失败：\n" + ex.Message + "\n请确认已安装 SQL Server 并执行了 init_db.sql 脚本。", "数据库错误");
                return false;
            }
        }

        // 【非查询操作】：执行增、删、改（Insert/Update/Delete）SQL 指令
        public static int ExecuteNonQuery(string sql, params SqlParameter[] parameters)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        if (parameters != null) cmd.Parameters.AddRange(parameters);
                        return cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("SQL 执行错误：" + ex.Message);
                return -1;
            }
        }

        // 【数据集查询】：执行查询并返回 DataTable 格式的结果集，常用于 DataGridView 绑定
        public static DataTable ExecuteQuery(string sql, params SqlParameter[] parameters)
        {
            DataTable dt = new DataTable();
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        if (parameters != null) cmd.Parameters.AddRange(parameters);
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("查询执行错误：" + ex.Message);
            }
            return dt;
        }

        // 【标量查询】：查询并返回单个结果值（如 COUNT(*), SUM(Total) 等）
        public static object ExecuteScalar(string sql, params SqlParameter[] parameters)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        if (parameters != null) cmd.Parameters.AddRange(parameters);
                        return cmd.ExecuteScalar();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        // 【JSON 数据桥接】：将 SQL 查询结果直接转换为标准 JSON 字符串
        // 此逻辑专门为 Web 端交互设计，实现了轻量级的手动序列化以减小库依赖
        public static string ExecuteJsonQuery(string sql, params SqlParameter[] parameters)
        {
            try
            {
                DataTable dt = ExecuteQuery(sql, parameters);
                if (dt == null || dt.Rows.Count == 0)
                    return "[]";

                StringBuilder json = new StringBuilder();
                json.Append("[");

                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    json.Append("{");
                    for (int j = 0; j < dt.Columns.Count; j++)
                    {
                        json.Append("\"").Append(dt.Columns[j].ColumnName).Append("\":");
                        
                        object value = dt.Rows[i][j];
                        if (value == null || value is DBNull)
                        {
                            json.Append("null");
                        }
                        else if (value is string || value is DateTime)
                        {
                            // 处理特殊转义字符，保证 Web 端解析不出错
                            json.Append("\"").Append(value.ToString().Replace("\"", "\\\"")).Append("\"");
                        }
                        else
                        {
                            json.Append(value.ToString());
                        }

                        if (j < dt.Columns.Count - 1)
                            json.Append(",");
                    }
                    json.Append("}");
                    if (i < dt.Rows.Count - 1)
                        json.Append(",");
                }

                json.Append("]");
                return json.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine("JSON 转换错误：" + ex.Message);
                return "[]";
            }
        }
    }
}
