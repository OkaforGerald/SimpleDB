using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SimpleDB.Extensions
{
    public static class JArrayExtensions
    {
        public static IDbCollection<T> ToDbCollection<T>(this JArray array)
        {
            DbCollection<T> list = new DbCollection<T>();

            foreach(var child in array.Children())
            {
                var json = child.ToString(Newtonsoft.Json.Formatting.None);

                var item = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true});

                list.Add(item);
            }

            return list;
        }
    }
}
