using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SimpleDB.Exceptions;
using SimpleDB.Extensions;
using Formatting = Newtonsoft.Json.Formatting;
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
                var schema = CreateSchema<T>();
                var properties = typeof(T).GetProperties();
                var prop = properties.FirstOrDefault(p => p.Name.Equals("id", StringComparison.OrdinalIgnoreCase));
                bool HasKey = prop is not null;

                var array = JArray.Parse(JsonSerializer.Serialize(new object[] { }));
                if (HasKey && prop?.PropertyType == typeof(int))
                {
                    string json = JsonSerializer.Serialize(new { Metadata = new Metadata { UsedIds = new HashSet<int> { }, MaxId = 1 } }, JsonSerializerOptions);
                    array.Add(schema);
                    array.Add(JObject.Parse(json));
                }
                else
                {
                    array.Add(schema);
                }

                _data.Add(tablename, array);
                
                FileAccess.WriteJsonToDb(file, _data.ToString(Formatting.None));
            }
            else
            {
                throw new DuplicateKeyException("Table with the same name already exists!");
            }
        }

        public void InsertOne<T>(T obj)
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
            dynamic NextKey;
            // If object has an Id but didn't explicitly give one
            if(HasKey && prop?.PropertyType == typeof(int) && (int)JObject.Parse(json)["id"] == 0 ||
                HasKey && prop?.PropertyType == typeof(Guid) && (Guid)JObject.Parse(json)["id"] == Guid.Empty ||
                HasKey && prop?.PropertyType == typeof(string) && (string)JObject.Parse(json)["id"] == null)
            {
                // Get next value for Id
                if (prop?.PropertyType == typeof(int))
                {
                    var metaObject = GetMetadata(tablename).ToString(Formatting.None);
                    var metadataJson = metaObject.Remove(0, 1).Remove(metaObject.Length - 2).Remove(0, 11);
                    var metadata = JsonSerializer.Deserialize<Metadata>(metadataJson, JsonSerializerOptions);

                    NextKey = metadata.MaxId;
                    metadata.MaxId++;
                    if (!data.Children().Any() && !PendingChanges.Where(x => x.Action == DbAction.Create).Any())
                    {
                        if(metadata.UsedIds is null)
                        {
                            metadata.UsedIds = new HashSet<int> { NextKey };
                        }
                        else
                        {
                            metadata.UsedIds.Add(NextKey);
                        }
                        
                    }else if (data.Children().Any())
                    {
                        metadata.UsedIds.Add(NextKey);
                    }
                    else
                    {
                        metadata.UsedIds.Add(NextKey);
                    }
                    SetMetadata(tablename, metadata);
                }
                else if(prop?.PropertyType == typeof(Guid))
                {
                    NextKey = Guid.NewGuid();
                }
                else
                {
                    NextKey = Guid.NewGuid().ToString();
                };

                if (data.Any(x => (dynamic)x["id"] == NextKey))
                {
                    throw new DuplicateKeyException($"{typeof(T)} with Id {NextKey} already Exists!");
                }

                var NewAddition = JObject.Parse(json);
                NewAddition["id"] = NextKey;
                var invalidProps = ValidateAgainstSchema<T>(JsonSerializer.Deserialize<T>(NewAddition.ToString(Formatting.None), JsonSerializerOptions));
                if(invalidProps is not null)
                {
                    throw new Exception(invalidProps);
                }

                PendingChanges.Enqueue(new PendingCommits(tablename, DbAction.Create, data: NewAddition));
            }
            else
            {
                dynamic key;
                var oldId = JObject.Parse(json)["id"];

                if (prop?.PropertyType == typeof(Guid))
                {
                    key = (Guid)oldId;
                }
                else if (prop?.PropertyType == typeof(int))
                {
                    var metaObject = GetMetadata(tablename).ToString(Formatting.None);
                    var metadataJson = metaObject.Remove(0, 1).Remove(metaObject.Length - 2).Remove(0, 11);
                    var metadata = JsonSerializer.Deserialize<Metadata>(metadataJson, JsonSerializerOptions);
                    key = (int)oldId;

                    if (metadata?.UsedIds.Contains(key))
                    {
                        throw new DuplicateKeyException($"{typeof(T)} with Id {key} already Exists!");
                    }
                    metadata.MaxId = key >= metadata.MaxId ? key + 1: metadata.MaxId++;
                    metadata.UsedIds.Add(key);
                    SetMetadata(tablename, metadata);
                }
                else
                {
                    key = (dynamic)oldId;
                }

                if (data.Any(x => (dynamic)x["id"] == key) || PendingChanges.Any(x => (dynamic)x.data["id"] == key))
                {
                    throw new DuplicateKeyException($"{typeof(T)} with Id {key} already Exists!");
                }

                var invalidProps = ValidateAgainstSchema<T>(JsonSerializer.Deserialize<T>(json, JsonSerializerOptions));
                if (invalidProps is not null)
                {
                    throw new Exception(invalidProps);
                }

                PendingChanges.Enqueue(new PendingCommits(tablename, DbAction.Create, data: JObject.Parse(json)));
            }
        }

        public void InsertMultiple<T>(IEnumerable<T> items)
        {
            if (items == null)
            {
                throw new ArgumentException();
            }

            foreach(var item in items)
            {
                InsertOne<T>(item);
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
                var metaObject = GetMetadata(tablename);
                if (metaObject is not null)
                {
                    var mObj = metaObject.ToString(Formatting.None);
                    var metadataJson = mObj.Remove(0, 1).Remove(mObj.Length - 2).Remove(0, 11);
                    var metadata = JsonSerializer.Deserialize<Metadata>(metadataJson, JsonSerializerOptions);

                    var id = (int)JObject.Parse(json)["id"];
                    metadata?.UsedIds.Remove(id);
                    SetMetadata(tablename, metadata);
                }
                newData = newData.Replace($"{json}", "").Replace(",,", ",").Replace("[,", "[").Replace(",]", "]");
            }
            
            PendingChanges.Enqueue(new PendingCommits(tablename, DbAction.Delete, Array: JArray.Parse(newData)));
        }

        public void DeleteOne<T>(dynamic id)
        {
            var tablename = typeof(T).Name.ToLower();
            data = GetTableJson(tablename);
            var metaObject = GetMetadata(tablename);

            var item = data.FirstOrDefault(x => x["id"] == id);

            if(item is not null)
            {
                if (metaObject is not null)
                {
                    var mObj = metaObject.ToString(Formatting.None);
                    var metadataJson = mObj.Remove(0, 1).Remove(mObj.Length - 2).Remove(0, 11);
                    var metadata = JsonSerializer.Deserialize<Metadata>(metadataJson, JsonSerializerOptions);

                    metadata?.UsedIds.Remove(id);
                    SetMetadata(tablename, metadata);
                }
                item.Remove();
            }

            PendingChanges.Enqueue(new PendingCommits(tablename, DbAction.Delete, Array: data));
        }

        public void UpdateByCondition<T>(Func<T, bool> predicate, T replacement)
        {
            var items = FindByCondition<T>(predicate);
            
            var metaObject = GetMetadata(typeof(T).Name.ToLower());
            var properties = typeof(T).GetProperties();
            var prop = properties.FirstOrDefault(p => p.Name.Equals("id", StringComparison.OrdinalIgnoreCase));
            bool HasKey = prop is not null;

            dynamic Key;

            var newData = data.ToString(Formatting.None);

            foreach(var item in items)
            {
                var replacementJson = JsonSerializer.Serialize(replacement, JsonSerializerOptions);
                var oldJson = JsonSerializer.Serialize(item, JsonSerializerOptions);
                var oldID = JObject.Parse(oldJson)["id"];

                if (HasKey && prop?.PropertyType == typeof(int) && (int)JObject.Parse(replacementJson)["id"] == 0 ||
                HasKey && prop?.PropertyType == typeof(Guid) && (Guid)JObject.Parse(replacementJson)["id"] == Guid.Empty)
                {
                    if (prop.PropertyType == typeof(int))
                    {
                        Key = (int)oldID;
                    }else if(prop.PropertyType == typeof(Guid))
                    {
                        Key = (Guid)oldID;
                    }
                    else
                    {
                        Key = (dynamic)oldID;
                    }

                    var update = JObject.Parse(replacementJson);
                    update["id"] = Key;
                    replacementJson = update.ToString(Formatting.None);
                }
                else
                {
                    var newId = (dynamic)JObject.Parse(replacementJson)["id"];

                    if (newId != oldID)
                    {
                        if (metaObject is not null)
                        {
                            var mObj = metaObject.ToString(Formatting.None);
                            var metadataJson = mObj.Remove(0, 1).Remove(mObj.Length - 2).Remove(0, 11);
                            var metadata = JsonSerializer.Deserialize<Metadata>(metadataJson, JsonSerializerOptions);

                            if(metadata.UsedIds.Contains((int)newId)) throw new DuplicateKeyException($"{typeof(T)} with Id {newId} already Exists!");
                            if (PendingChanges.Any(x => (int)x.data["id"] == (int)newId)) throw new DuplicateKeyException($"{typeof(T)} with Id {newId} already Exists!");
                            metadata.UsedIds.Add((int)newId);
                            metadata.UsedIds.Remove((int)oldID);
                            metadata.MaxId = metadata.MaxId >= (int)newId ? metadata.MaxId + 1 : (int)newId + 1;
                            SetMetadata(typeof(T).Name.ToLower(), metadata);
                        }
                        else
                        {
                            if (data.Any(x => (dynamic)x["id"] == newId) || PendingChanges.Any(x => (dynamic)x.data["id"] == newId)) throw new DuplicateKeyException($"{typeof(T)} with Id {newId} already Exists!");
                        }
                    }
                }
                    newData = newData.Replace(oldJson, replacementJson).Replace(",,", ",").Replace("[,", "[").Replace(",]", "]");
            }

            PendingChanges.Enqueue(new PendingCommits(typeof(T).Name.ToLower(), DbAction.Update, Array: JArray.Parse(newData)));
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
                        case DbAction.Update:
                            CommitDelete(change.table, change.Array);
                            break;
                    }
                }

                    try
                    {
                        FileAccess.WriteJsonToDb(file, _data.ToString(Formatting.None));
                    }
                    catch (Exception ex)
                    {
                    Console.WriteLine(ex.Message);
                    }

                IsSuccessful = true;
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

            if (GetMetadata(table) is not null)
            {
                tableDb.Add(GetMetadata(table));
            }
            _data[table] = tableDb;
        }

        private void CommitDelete(string table, JArray NewArray)
        {
            var meta = GetMetadata(table);
            if(meta is not null)
            {
                NewArray.Add(meta);
            }
            _data[table] = NewArray;
        }

        private void CommitUpdate(string table, JArray NewArray)
        {
            var meta = GetMetadata(table);
            if (meta is not null)
            {
                NewArray.Add(meta);
            }
            _data[table] = NewArray;
        }

        private JArray GetTableJson(string tablename)
        {
            lock (_data)
            {
                var table = _data[tablename];

                var json = JArray.Parse(table.ToString(Formatting.None));
                var meta = json.FirstOrDefault(x => x["metadata"] != null);
                if (meta != null) { meta.Remove(); }
                return json;
            }
        }

        private JObject? GetMetadata(string tablename)
        {
            lock (_data)
            {
                var table = _data[tablename];

                var json = JArray.Parse(table.ToString(Formatting.None));
                var response = json.FirstOrDefault(x => x["metadata"] != null)?.ToString(Formatting.None);
                
                if(response is not null)
                {
                    return JObject.Parse(response);
                }
                else
                {
                    return null;
                }
            }
        }

        private void SetMetadata(string tablename, Metadata metadata)
        {
            lock (_data)
            {
                var oldMeta = GetMetadata(tablename)?.ToString(Formatting.None);

                var newMeta = JsonSerializer.Serialize(new { Metadata = metadata }, JsonSerializerOptions);

                _data[tablename] = JArray.Parse(_data[tablename].ToString(Formatting.None).Replace(oldMeta, newMeta));

                FileAccess.WriteJsonToDb(file, _data.ToString(Formatting.None));
            }
        }

        private JObject CreateSchema<T>()
        {
            var properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);
            var Schema = new ExpandoObject();
            foreach (var property in properties)
            {
                Type type = property.PropertyType;
                if (type.IsAnsiClass)
                {
                    var defaultExp = Expression.Default(type);
                    var c = Expression.Lambda(defaultExp).Compile().DynamicInvoke();

                    var propName = property.Name;
                    if (c is null)
                    {
                        //nullable property
                        Schema.TryAdd(propName.ToLower(), "nullable");
                    }
                    else
                    {
                        //required property
                        Schema.TryAdd(propName.ToLower(), "required");
                    }
                }
            }
            var schemaJson = JsonSerializer.Serialize(Schema, JsonSerializerOptions);
            return JObject.Parse(schemaJson);
        }

        private bool ValidateAgainstSchema<T>(T obj)
        {
            throw new NotImplementedException();
        }

        private bool TableExists(string table)
        {
            return _data.TryGetValue(table, out _);
        }

        enum DbAction
        {
            Create, Update, Delete
        }

        record PendingCommits(string table, DbAction Action, JObject? data = null, JArray? Array = null);
    }
}
