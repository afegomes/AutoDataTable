using System;

namespace AutoDataTable.Core
{
    [AttributeUsage(AttributeTargets.Method)]
    public class GenerateDataTableAttribute : Attribute
    {
        public GenerateDataTableAttribute(Type type)
        {
            Type = type;
        }

        public Type Type { get; }
    }
}