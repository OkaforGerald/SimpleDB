using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SimpleDB
{
    public class FileAccess
    {
        public static string GetJsonFromDb(string filename)
        {
            var path = $"{Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName}\\{filename}";

            if (!File.Exists(path))
            {
                var stream = File.Open(path, FileMode.Create);

                stream.Dispose();

                File.WriteAllText(path, "[]");
            }

            return File.ReadAllText(path);
        }

        public static void WriteJsonToDb(string filename, string json)
        {
            var path = $"{Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName}\\{filename}";

            if (!File.Exists(path))
            {
                var stream = File.Open(path, FileMode.Create);

                stream.Dispose();
            }

            File.WriteAllText(path, json);
        }
    }
}
