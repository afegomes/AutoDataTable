using System;

namespace AutoDataTable.Core
{
    [AttributeUsage(AttributeTargets.Property)]
    public class DataColumnAttribute : Attribute
    {
        public DataColumnAttribute(string columnName)
        {
            ColumnName = columnName;
        }

        public string ColumnName { get; private set; }
    }
}