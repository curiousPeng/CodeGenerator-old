using Dapper;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using Oracle.ManagedDataAccess.Client;
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
            // 添加json配置文件路径 nuget 引用 Microsoft.Extensions.Configuration; Microsoft.Extensions.Configuration.FileExtensions;Microsoft.Extensions.Configuration.Json;
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

        protected static OracleConnection GetOpenConnection()
        {
            var connection = new OracleConnection(ConnectionString);
            connection.Open();
            return connection;
        }

        protected static PageDataView<T> Paged<T>(
            string tableName,
            string where,
            int pageSize,
            int currentPage,
            string orderBy="")
        {
            var result = new PageDataView<T>();
            var count_sql = string.Format("SELECT COUNT(1) FROM {0}", tableName);

            var whereIn = "WHERE 1=1 ";
            if (!string.IsNullOrWhiteSpace(where))
            {
                if (where.ToLower().Contains("where"))
                {
                    throw new ArgumentException("where子句不需要带where关键字");
                }
                whereIn = string.Format("{0} and {1}", whereIn, where);
            }
            var startNum = (currentPage - 1) * pageSize;
            var endNum = currentPage * pageSize;
            var sql = string.Format("SELECT * FROM (SELECT tt.*, ROWNUM AS rowno FROM(SELECT t.* FROM {1} WHERE {2} ORDER BY {0})tt WHERE ROWNUM <= {4}) " + "table_alias WHERE table_alias.rowno > {4} ", orderBy, tableName, where, startNum, endNum);
            if (string.IsNullOrWhiteSpace(orderBy))
            {
                sql = string.Format("SELECT * FROM (SELECT t.*,ROWNUM AS rowno FROM {1} t {2} and ROWNUM <= {4}) table_alias WHERE table_alias.rowno > {3} ", orderBy, tableName, whereIn, startNum, endNum);
            }
            count_sql += whereIn;
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
