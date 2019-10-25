using Dapper;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using MySql.Data.MySqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace DataLayer.Base
{
    public abstract class BaseTableHelper
    {
        public static string ConnectionString { get; }

        static BaseTableHelper()
        {
            // 添加json配置文件路径
#if LOCAL
            var builder = new ConfigurationBuilder().SetBasePath(System.AppDomain.CurrentDomain.BaseDirectory).AddJsonFile("appsettings.Local.json");
#elif DEBUG
            var builder = new ConfigurationBuilder().SetBasePath(System.AppDomain.CurrentDomain.BaseDirectory).AddJsonFile("appsettings.Development.json");
#else
            var builder = new ConfigurationBuilder().SetBasePath(System.AppDomain.CurrentDomain.BaseDirectory).AddJsonFile("appsettings.json");
#endif
            // 创建配置根对象
            var configurationRoot = builder.Build();
            ConnectionString = configurationRoot.GetSection("DbConnect:ConnectString").Value;
        }

        protected static MySqlConnection GetOpenConnection(bool mars = false)
        {
            var cs = ConnectionString;
            if (mars)
            {
                var scsb = new MySqlConnectionStringBuilder(cs)
                {
                    AllowBatch = true
                };
                cs = scsb.ConnectionString;
            }
            var connection = new MySqlConnection(cs);
            connection.Open();
            return connection;
        }

        protected static PageDataView<T> Paged<T>(
            string tableName,
            string where,
            string orderBy,
            string columns,
            int pageSize,
            int currentPage, string primaryKey)
        {
            var result = new PageDataView<T>();
            var count_sql = string.Format("SELECT COUNT(1) FROM {0}", tableName);
            
            if (!string.IsNullOrWhiteSpace(where))
            {
                if (where.ToLower().Contains("where"))
                {
                    throw new ArgumentException("where子句不需要带where关键字");
                }
                where = " WHERE " + where;
            }
            var pageStart = (currentPage - 1) * pageSize;
            if (string.IsNullOrWhiteSpace(orderBy))
            {
                var sql = string.Format("SELECT {0} FROM {2} where {6} >=(select {6} from {2} {3}  limit {4},1) limit {5}; ", columns, "", tableName, where, pageStart, pageSize, primaryKey);
            }
            else
            {
                var sql = string.Format("SELECT {0} FROM {2} where {6} >=(select {6} from {2} {3}  ORDER BY {1} limit {4},1) limit {5}; ", columns, orderBy, tableName, where, pageStart, pageSize, primaryKey);
            }
            
            count_sql += where;
            using (var conn = GetOpenConnection())
            {
                result.TotalRecords = conn.ExecuteScalar<int>(count_sql);
                result.TotalPages = result.TotalRecords / pageSize;
                if (result.TotalRecords % pageSize > 0)
                    result.TotalPages += 1;
                var list = conn.Query<T>(sql);
                result.Items = list.Count() == 0 ? (new List<T>()) : list.ToList();
            }

            return result;
        }
    }
}
