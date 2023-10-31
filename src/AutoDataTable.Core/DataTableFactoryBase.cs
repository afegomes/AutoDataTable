using System.Data;

namespace AutoDataTable.Core
{
    public abstract class DataTableFactory
    {
        public abstract DataTable Create();
    }
}