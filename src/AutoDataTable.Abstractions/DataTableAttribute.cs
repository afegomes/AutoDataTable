using System;

namespace AutoDataTable.Abstractions
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DataTableAttribute : Attribute
    {
        public DataTableAttribute()
        {
        }

        public DataTableAttribute(string tableName)
        {
            TableName = tableName;
        }

        public string TableName { get; private set; }
    }
}