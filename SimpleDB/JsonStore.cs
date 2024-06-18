using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SimpleDB.Exceptions;
using SimpleDB.Extensions;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SimpleDB
{
    public class JsonStore
    {
        private readonly string file;
        private JsonSerializerOptions JsonSerializerOptions;
        private JArray data;
        private JObject _data;
        private Queue<PendingCommits> PendingChanges = new Queue<PendingCommits>();

        public JsonStore(string file)
        {
            this.file = file;
            _data = JObject.Parse(FileAccess.GetJsonFromDb(file));
            JsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        public void CreateTable<T>()
        {
            var tablename = typeof(T).Name.ToLower();

            var tableExists = TableExists(tablename);

            if (!tableExists)
            {
                var properties = typeof(T).GetProperties();
                var prop = properties.FirstOrDefault(p => p.Name.Equals("id", StringComparison.OrdinalIgnoreCase));
                bool HasKey = prop is not null;

                var array = JArray.Parse(JsonSerializer.Serialize(new object[] { }));
                if (HasKey && prop?.PropertyType == typeof(int))
                {
                    string json = JsonSerializer.Serialize(new { Metadata = new Metadata { UsedIds = new HashSet<int> { }, MaxId = 1 } }, JsonSerializerOptions);
                    array.Add(JObject.Parse(json));
                }

                _data.Add(tablename, array);
                
                FileAccess.WriteJsonToDb(file, _data.ToString(Formatting.None));
            }
            else
            {
                throw new DuplicateKeyException("Table with the same name already exists!");
            }
        }

        public void Insert<T>(T obj) where T : class
        {
            var tablename = typeof(T).Name.ToLower();
            if (obj is null)
            {
                throw new ArgumentException();
            }

            var tableExists = TableExists(tablename);

            if(!tableExists)
            {
                throw new NotFoundException($"Database {tablename} can not be found");
            }

            data = GetTableJson(tablename);

            var json = JsonSerializer.Serialize(obj, JsonSerializerOptions);

            var properties = typeof(T).GetProperties();
            var prop = properties.FirstOrDefault(p => p.Name.Equals("id", StringComparison.OrdinalIgnoreCase));
            bool HasKey = prop is not null;
            if (HasKey)
            {
                var usedIds = JsonSerializer.Serialize(new HashSet<int> { }, JsonSerializerOptions);
            }
            dynamic NextKey;
            // If object has an Id but didn't explicitly give one
            if(HasKey && prop.PropertyType == typeof(int) && (int)JObject.Parse(json)["id"] == 0 ||
                HasKey && prop.PropertyType == typeof(Guid) && (Guid)JObject.Parse(json)["id"] == Guid.Empty)
            {
                // Get next value for Id
                if (prop?.PropertyType == typeof(int))
                {
                    if (!data.Children().Any() && !PendingChanges.Where(x => x.Action == DbAction.Create).Any())
                    {
                        NextKey = 1;
                    }
                    else
                    {
                        if (!data.Children().Any())
                        {
                            var LastAddition = PendingChanges.LastOrDefault(x => x.Action == DbAction.Create);
                            var NextKeyFromQueue = (int)LastAddition?.data["id"] + 1;

                            NextKey = NextKeyFromQueue;
                        }
                        else
                        {
                            var prev = (int)data.Last["id"];
                            var NextKeyFromDb = prev + 1;

                            var LastAddition = PendingChanges.LastOrDefault(x => x.Action == DbAction.Create);
                            if (LastAddition is null)
                            {
                                NextKey = NextKeyFromDb;
                            }
                            else
                            {
                                var NextKeyFromQueue = (int)LastAddition?.data["id"] + 1;

                                NextKey = NextKeyFromQueue > NextKeyFromDb ? NextKeyFromQueue : NextKeyFromDb;
                            }
                        }
                    }
                }
                else if(prop?.PropertyType == typeof(Guid))
                {
                    NextKey = Guid.NewGuid();
                }
                else
                {
                    NextKey = Guid.NewGuid().ToString();
                };

                if (data.Any(x => (dynamic)x["id"] == NextKey) || PendingChanges.Any(x => (dynamic)x.data["id"] == NextKey))
                {
                    throw new DuplicateKeyException($"{typeof(T)} with Id {NextKey} already Exists!");
                }

                var NewAddition = JObject.Parse(json);
                NewAddition["id"] = NextKey;

                PendingChanges.Enqueue(new PendingCommits(tablename, DbAction.Create, data: NewAddition));
            }
            else
            {
                dynamic key;

                if (prop?.PropertyType == typeof(Guid))
                {
                    key = (Guid)JObject.Parse(json)["id"];
                }
                else
                {
                    key = (dynamic)JObject.Parse(json)["id"];
                }

                if (data.Any(x => (dynamic)x["id"] == key) || PendingChanges.Any(x => (dynamic)x.data["id"] == key))
                {
                    throw new DuplicateKeyException($"{typeof(T)} with Id {key} already Exists!");
                }

                PendingChanges.Enqueue(new PendingCommits(tablename, DbAction.Create, data: JObject.Parse(json)));
            }
        }

        public ICollection<T> FindAll<T>()
        {
            var tablename = typeof(T).Name.ToLower();
            var tableExists = TableExists(tablename);

            if (!tableExists)
            {
                throw new NotFoundException($"Database {tablename} can not be found");
            }
            data = GetTableJson(tablename);
            return data.ToList<T>();
        }

        public IEnumerable<T>? FindByCondition<T>(Func<T, bool> predicate)
        {
            var tablename = typeof(T).Name.ToLower();
            var tableExists = TableExists(tablename);

            if (!tableExists)
            {
                throw new NotFoundException($"Database {tablename} can not be found");
            }

            data = GetTableJson(tablename);
            var response = data.ToList<T>().Where<T>(predicate);
            
            if(!response.Any())
            {
                throw new NotFoundException($"{typeof(T)} which fits the condition does not exist!");
            }
            else
            {
                return response;
            }
        }
        
        public void DeleteByCondition<T>(Func<T, bool> predicate)
        {
            var tablename = typeof(T).Name.ToLower();
            data = GetTableJson(tablename);

            var items = FindByCondition<T>(predicate);

            string newData = data.ToString(Formatting.None);

            foreach(var item in items)
            {
                string json = JsonSerializer.Serialize(item, JsonSerializerOptions);
                newData = newData.Replace($"{json}", "").Replace(",,", ",").Replace("[,", "[").Replace(",]", "]");
            }
            var c = JsonSerializer.Serialize(newData, JsonSerializerOptions);
            
            PendingChanges.Enqueue(new PendingCommits(tablename, DbAction.Delete, Array: JArray.Parse(newData)));
        }

        public bool Commit()
        {
            bool IsSuccessful = false;
            lock (PendingChanges)
            {
                while (PendingChanges.Count > 0)
                {
                    var change = PendingChanges.Dequeue();

                    switch (change.Action)
                    {
                        case DbAction.Create:
                            CommitCreate(change.table, change.data);
                            break;
                        case DbAction.Delete:
                            CommitDelete(change.table, change.Array);
                            break;

                    }
                }

                    try
                    {
                        FileAccess.WriteJsonToDb(file, _data.ToString(Formatting.None));

                        IsSuccessful = true;
                    }
                    catch (Exception ex)
                    {
                    Console.WriteLine(ex.Message);
                    }
                return IsSuccessful;
            }
        }

        private void CommitCreate(string table, JObject NewAddition)
        {
            var tableDb = GetTableJson(table);

            if (tableDb.Parent is null)
            {
                tableDb.Add(NewAddition);
            }
            else
            {
                tableDb.AddAfterSelf(NewAddition);
            }

            tableDb.Add(GetMetadata(table));
            _data[table] = tableDb;
        }

        private void CommitDelete(string table, JArray NewArray)
        {
            _data[table] = NewArray;
        }

        private JArray GetTableJson(string tablename)
        {
            var table = _data[tablename];

            var json = JArray.Parse(table.ToString(Formatting.None));
            json.Where(x => x["metadata"] != null).FirstOrDefault()?.Remove();
            return json;
        }

        private JObject GetMetadata(string tablename)
        {
            var table = _data[tablename];

            var json = JArray.Parse(table.ToString(Formatting.None));
            var response = json.Where(x => x["metadata"] != null).FirstOrDefault()?.ToString(Formatting.None);

            return JObject.Parse(response);
        }

        private bool TableExists(string table)
        {
            return _data.TryGetValue(table, out JToken array);
        }

        enum DbAction
        {
            Create, Update, Delete
        }

        record PendingCommits(string table, DbAction Action, JObject? data = null, JArray? Array = null);
    }
}
