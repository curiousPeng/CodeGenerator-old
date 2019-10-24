using System;
using System.Collections.Generic;
using Dapper;
using System.Text;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Console
{
   public class TableColumns
    {
        [Column("COLUMN_NAME")]
        public string column_name { get; set; }
        [Column("DATA_TYPE")]
        public string data_type { get; set; }
        [Column("DATA_LENGTH")]
        public int data_length { get; set; }
        [Column("COMMENTS")]
        public string comments { get; set; }
        [Column("NULLABLE")]
        public string nullable { get; set; }
    }
    public class TableFileds
    {
        [Column("COMMENTS")]
        public string comments { get; set; }
        [Column("TABLE_NAME")]
        public string table_name { get; set; }
    }
    public class TablePKColumn
    {
        [Column("COLUMN_NAME")]
        public string column_name { get; set; }
        [Column("TABLE_NAME")]
        public string table_name { get; set; }
    }
}
