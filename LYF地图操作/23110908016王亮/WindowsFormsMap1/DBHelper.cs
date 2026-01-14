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
        // 连接字符串：连接本地默认 SQL Server 实例，使用 Windows 身份验证
        // [用户指定] 实例名: .\SQLEXPRESS
        private static readonly string ConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=ICH_VisualDB;Integrated Security=True";

        // 测试连接
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
                MessageBox.Show("数据库连接失败: " + ex.Message + "\n请确认已执行 init_db.sql 并且服务已启动。", "DB Error");
                return false;
            }
        }

        // 执行增删改 (Insert/Update/Delete)
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
                Console.WriteLine("SQL Error: " + ex.Message);
                return -1;
            }
        }

        // 执行查询返回 DataTable
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
                Console.WriteLine("Query Error: " + ex.Message);
            }
            return dt;
        }

        // 查询单个值 (例如 Count)
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

        // [Member E] Added: 执行查询并返回JSON字符串（手动序列化，不依赖Newtonsoft）
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
                Console.WriteLine("ExecuteJsonQuery Error: " + ex.Message);
                return "[]";
            }
        }
    }
}
