using System;

namespace AutoDataTable.Core
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
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