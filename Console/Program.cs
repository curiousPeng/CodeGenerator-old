using Dapper;
using Generator.Core;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
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

            //ReCreateDB(conn_str, Encoding.GetEncoding("gb2312"));

            var dbcode = FindDBName(conn_str);
            Print("解析数据库元数据...");
            using (MySqlConnection connection = new MySqlConnection(conn_str))
            {
                var config = new SQLMetaData();
                SQLMetaDataHelper.InitConfig(config);

                var CommandText = "SHOW TABLES;";
                connection.Open();
                List<string> TableNames = connection.Query<string>(CommandText).ToList();

                foreach (var name in TableNames)
                {
                    var table = new TableMetaData
                    {
                        Name = name,
                        Columns = new List<ColumnMetaData>(),
                        Comment = "",
                        ExistPredicate = new List<ColumnMetaData>(),
                        Identity = new ColumnMetaData(),
                        PrimaryKey = new List<ColumnMetaData>(),
                        WherePredicate = new List<ColumnMetaData>()
                    };//准备创建字段信息

                    CommandText = "SHOW FULL FIELDS FROM " + name + ";";//得到表结构
                    var result = connection.Query<FieldViewModel>(CommandText).ToList();
                    bool _isfail = false;
                    foreach (var item in result)
                    {
                        item.Type = item.Type.Substring(0, item.Type.IndexOf("("));
                        if (item.Type.Any(p => p.Equals("enum") || p.Equals("set")))
                        {
                            Print($"表{name}存在enum和set字段，暂无对应数据结构，跳过此表的生成！");
                            _isfail = true;
                            break;
                        }
                        var ColumData = new ColumnMetaData
                        {
                            Comment = item.Comment,
                            DbType = SQLMetaDataHelper.MapCsharpType(item.Type),
                            HasDefaultValue = item.Default != null ? true : false,
                            IsIdentity = item.Key == "PRI" ? true : false,
                            IsPrimaryKey = item.Key == "PRI" ? true : false,
                            Name = item.Field,
                            Nullable = item.Null == "YES" ? true : false
                        };

                        if (item.Key == "PRI")
                        {
                            table.PrimaryKey.Add(ColumData);
                            table.WherePredicate.Add(ColumData);
                            table.ExistPredicate.Add(ColumData);
                        }
                        if (item.Extra == "auto_increment")
                        {
                            table.Identity = ColumData;
                        }
                        table.Columns.Add(ColumData);
                    }
                    if (!_isfail)
                    {
                        config.Tables.Add(table);
                    }
                }
                connection.Close();

                // 解析数据库元数据
                //var parser = new MetaDataParser(config);
                //parser.Parse(data);
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
                        Print("生成Enum...");
                        SQLMetaDataHelper.OutputEnum(config);

                        // 检测partial字段有效性
                        Print("检测partial字段有效性...");
                        SQLMetaDataHelper.DoPartialCheck(config);

                        Print("生成完毕！");
                        break;
                    }
                    System.Console.WriteLine("按‘quit’退出");
                } while (key != "quit");
            }

            Print("结束！");
            System.Console.Read();
            Environment.Exit(0);
        }

        static string FindDBName(string connStr)
        {
            var db_name = string.Empty;
            var cb = new DbConnectionStringBuilder(false);
            cb.ConnectionString = connStr;
            object database;
            if (cb.TryGetValue("Initial Catalog", out database))
            {
                db_name = database.ToString();
            }
            else
            {
                if (cb.TryGetValue("Database", out database))
                {
                    db_name = database.ToString();
                }
            }

            return db_name;
        }


        static void Print(string message)
        {
            System.Console.WriteLine();
            System.Console.WriteLine();
            System.Console.WriteLine(message);
        }
    }
}
