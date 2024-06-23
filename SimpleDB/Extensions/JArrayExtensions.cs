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
        public static ICollection<T> ToList<T>(this JArray array)
        {
            var list = new List<T>();

            var meta = array.FirstOrDefault(x => x["metadata"] != null);
            if (meta != null) { meta.Remove(); }

            foreach (var child in array.Children())
            {
                if(child == array.First)
                {
                    continue;
                }

                var json = child.ToString(Newtonsoft.Json.Formatting.None);

                var item = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true});

                list.Add(item);
            }

            return list;
        }
    }
}
