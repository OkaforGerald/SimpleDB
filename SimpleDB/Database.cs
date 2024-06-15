using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SimpleDB.Exceptions;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SimpleDB
{
    public class Database
    {
        private readonly string file;
        private JsonSerializerOptions JsonSerializerOptions;
        private JArray data;
        private Queue<PendingCommits> PendingChanges = new Queue<PendingCommits>();

        public Database(string file)
        {
            this.file = file;
            data = JArray.Parse(FileAccess.GetJsonFromDb(file) ?? "");
            JsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        public void Insert<T>(T obj) where T : class
        {
            if (obj is null)
            {
                throw new ArgumentException();
            }

            var json = JsonSerializer.Serialize(obj, JsonSerializerOptions);

            var properties = typeof(T).GetProperties();
            var prop = properties.FirstOrDefault(p => p.Name.Equals("id", StringComparison.OrdinalIgnoreCase));
            bool HasKey = prop is not null;
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

                PendingChanges.Enqueue(new PendingCommits(NewAddition, DbAction.Create));
            }
            else
            {
                var key = (dynamic)JObject.Parse(json)["id"];

                if (data.Any(x => (dynamic)x["id"] == key) || PendingChanges.Any(x => (dynamic)x.data["id"] == key))
                {
                    throw new DuplicateKeyException($"{typeof(T)} with Id {key} already Exists!");
                }

                PendingChanges.Enqueue(new PendingCommits(JObject.Parse(json), DbAction.Create));
            }
        }

        public bool Commit()
        {
            bool IsSuccessful = false;
            lock (PendingChanges)
            {
                while (PendingChanges.Count > 0)
                {
                    var change = PendingChanges.Dequeue();

                    IsSuccessful = change.Action switch
                    {
                        DbAction.Create => CommitCreate(change.data),
                        _ => false
                    };
                }
            }
            return IsSuccessful;
        }

        private bool CommitCreate(JObject NewAddition)
        {
            if (data.Parent is null)
            {
                data.Add(NewAddition);
            }
            else
            {
                data.AddAfterSelf(NewAddition);
            }
            try
            {
                FileAccess.WriteJsonToDb(file, data.ToString(Formatting.None));

                return true;
            }catch (Exception ex)
            {
                return false;
            }
        }

        enum DbAction
        {
            Create, Update, Delete
        }

        record PendingCommits(JObject data, DbAction Action);
    }
}
