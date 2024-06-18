using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleDB
{
    public class Metadata
    {
        public HashSet<int> UsedIds { get; set; }

        public int MaxId { get; set; }
    }
}
