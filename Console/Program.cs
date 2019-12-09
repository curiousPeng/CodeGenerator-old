using Dapper;
using Generator.Common;
using Generator.Core;
using Newtonsoft.Json;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;

namespace Console
{
    class Program
    {
        static void Main(string[] args)
        {
            var conn_str = SQLMetaDataHelper.Config.DBConn;
            if (string.IsNullOrWhiteSpace(conn_str))
            {
                System.Console.WriteLine("未设置数据库连接字符串！");
                System.Console.Read();
                Environment.Exit(0);
            }

            Print("解析数据库元数据...");
            var config = new SQLMetaData();
            SQLMetaDataHelper.InitConfig(config);

            List<TableFileds> Tables = GetUserAllTables(conn_str);
            List<TablePKColumn> PKs = GetAllTablePKColumn(conn_str);
            // 解析数据库元数据
            IProgressBar progress = GetProgressBar();
            var parser = new MetaDataParser(progress);
            var count = 0;
            foreach (var table in Tables)
            {
                var tableData = new TableMetaData
                {
                    Name = table.table_name,
                    Columns = new List<ColumnMetaData>(),
                    Comment = table.comments,
                    ExistPredicate = new List<ColumnMetaData>(),
                    Identity = new ColumnMetaData(),
                    PrimaryKey = new List<ColumnMetaData>(),
                    WherePredicate = new List<ColumnMetaData>()
                };//准备创建字段信息

                bool _isfail = false;
                List<TableColumns> columns = GetTableColumns(table.table_name, conn_str);
                var pk = PKs.Where(p => p.table_name == table.table_name).FirstOrDefault();
                if (pk == null)
                {
                    pk = new TablePKColumn();
                    pk.column_name = "0";
                }
                foreach (var column in columns)
                {
                    if (column.data_type == "NUMBER")
                    {
                        if (column.data_length == 1)
                        {
                            column.data_type = "NUMBER(1)";
                        }
                    }

                    var ColumData = new ColumnMetaData
                    {
                        Comment = column.comments,

                        DbType = SQLMetaDataHelper.MapCsharpType(column.data_type),
                        HasDefaultValue = false,
                        IsIdentity = false,
                        IsPrimaryKey = pk.column_name == column.column_name ? true : false,
                        Name = column.column_name,
                        Nullable = column.nullable == "Y" ? true : false
                    };

                    if (pk.column_name == column.column_name)
                    {
                        tableData.PrimaryKey.Add(ColumData);
                        tableData.WherePredicate.Add(ColumData);
                        tableData.ExistPredicate.Add(ColumData);
                    }
                    tableData.Columns.Add(ColumData);
                }

                if (!_isfail)
                {
                    config.Tables.Add(tableData);
                }
                // 打印进度
                parser.ProgressPrint(++count, Tables.Count);
            }
           
            Print("解析完毕，生成中间配置文件...");
            // 生成中间配置文件
            var config_json_str = JsonConvert.SerializeObject(config);
            SQLMetaDataHelper.OutputConfig(config_json_str);

            // 生成最终文件
            Print("按 'y/Y' 继续生成最终操作类文件...");
            var key = string.Empty;
            do
            {
                key = System.Console.ReadLine();
                if (key == "Y" || key == "y")
                {
                    // 生成DAL最终文件
                    Print("生成DAL...");
                    SQLMetaDataHelper.OutputDAL(config);

                    // 生成Model最终文件
                    Print("生成Model...");
                    SQLMetaDataHelper.OutputModel(config);

                    // 生成Enum最终文件
                    // Print("生成Enum...");
                    // SQLMetaDataHelper.OutputEnum(config);

                    // 检测partial字段有效性
                    Print("检测partial字段有效性...");
                    SQLMetaDataHelper.DoPartialCheck(config);

                    Print("生成完毕！");
                    break;
                }
                System.Console.WriteLine("按‘quit’退出");
            } while (key != "quit");
            Print("结束！");
            System.Console.Read();
            Environment.Exit(0);
        }

        static void Print(string message)
        {
            System.Console.WriteLine();
            System.Console.WriteLine();
            System.Console.WriteLine(message);
        }

        /// <summary>
        /// 获取数据表名和备注
        /// </summary>
        /// <returns></returns>
        public static List<TableFileds> GetUserAllTables(string conn_str)
        {
            var sql = new StringBuilder();
            sql.Append("select a.TABLE_NAME, b.COMMENTS from user_tables a, user_tab_comments b");
            sql.Append(" where a.TABLE_NAME = b.TABLE_NAME order by TABLE_NAME");
            List<TableFileds> ret;
            using (var conn = GetOpenConnection(conn_str))
            {
                ret = conn.Query<TableFileds>(sql.ToString()).ToList();
            }

            return ret;
        }

        /// <summary>
        /// 获取到表的字段名，类型，长度和介绍
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public static List<TableColumns> GetTableColumns(string tableName, string conn_str)
        {
            var sql = new StringBuilder();
            sql.Append("SELECT a.column_name,a.data_type,a.data_length,b.comments,a.nullable");
            sql.Append(" FROM user_tab_columns a, user_col_comments b ");
            sql.Append(" WHERE a.table_name = b.table_name and b.column_name=a.column_name and a.table_name = :tableName");
            List<TableColumns> ret;
            using (var conn = GetOpenConnection(conn_str))
            {
                ret = conn.Query<TableColumns>(sql.ToString(), new { @tableName = tableName }).ToList();
            }

            return ret;
        }

        public static List<TablePKColumn> GetAllTablePKColumn(string conn_str)
        {
            var sql = new StringBuilder();
            sql.Append("select  col.table_name,col.column_name from user_constraints con,user_cons_columns col where con.constraint_name=col.constraint_name and con.constraint_type='P'");
            List<TablePKColumn> ret;
            using (var conn = GetOpenConnection(conn_str))
            {
                ret = conn.Query<TablePKColumn>(sql.ToString()).ToList();
            }

            return ret;
        }
        private static OracleConnection GetOpenConnection(string conn_str)
        {
            var connection = new OracleConnection(conn_str);
            connection.Open();
            return connection;
        }

        private static ConsoleProgressBar GetProgressBar()
        {
            return new ConsoleProgressBar(System.Console.CursorLeft, System.Console.CursorTop, 50, ProgressBarType.Character);
        }
    }
}
