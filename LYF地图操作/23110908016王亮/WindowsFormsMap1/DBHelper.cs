using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Windows.Forms;

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
        public static int ExecuteNonQuery(string sql, SqlParameter[] parameters = null)
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
        public static DataTable ExecuteQuery(string sql, SqlParameter[] parameters = null)
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
        public static object ExecuteScalar(string sql, SqlParameter[] parameters = null)
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
    }
}
