using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleDB
{
    public interface IDbCollection<T> : IList<T>
    {
        IEnumerable<T> FindByCondition(Func<T, bool> predicate);
    }
}
