using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using SimpleDB.Exceptions;

namespace SimpleDB
{
    public class DbCollection<T> : List<T>, IDbCollection<T>
    {
        public IEnumerable<T> FindByCondition(Func<T, bool> predicate)
        {
            var response = this.Where(predicate);

            if(response.Any())
            {
                return response;
            }
            else
            {
                throw new NotFoundException($"{typeof(T)} which matches the condition given does not exist!");
            }
        }
    }
}
