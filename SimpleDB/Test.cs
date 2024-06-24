using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleDB
{
    public class Employee
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Workplace { get; set; }

        public List<string> strings => new List<string> { "Omo", "mo", "o"};
    }

    public class EmployeeV2
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public string Workplace { get; set; }
    }

    public class EmployeeV3
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public string Workplace { get; set; }
    }

    public class EmployeeV4
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public bool IsOld { get; set; }
    }

    public class Test
    { 
        public static void Main(string[] args)
        {
            JsonStore v2 = new JsonStore("v2.json");
            try
            {
                //v2.CreateTable<EmployeeV3>();
                //v2.Insert<EmployeeV2>(new EmployeeV2 { Name = "Raighne", Workplace = "Google" });
                //v2.Insert<EmployeeV2>(new EmployeeV2 { Id = Guid.Parse("b79856bc-a4a6-407e-952e-16ffe105ddb2"), Name = "Onyeka", Workplace = "Google" });
                //v2.Insert<EmployeeV2>(new EmployeeV2 { Id = Guid.Parse("b79856bc-a4a6-407e-952e-16ffe105ddb2"), Name = "Onyeka", Workplace = "Google" });
                //v2.Insert<EmployeeV2>(new EmployeeV2 { Name = "Onyeka", Workplace = "Google" });

                //v2.CreateTable<Employee>();
                //v2.InsertOne<Employee>(new Employee { Id = 10, Name = "Raighne", Workplace = "Kuda" });
                //v2.InsertMultiple<Employee>(new List<Employee> { new Employee { Name = "Raighne", Workplace = "Lafarge" }, new Employee { Id = 10, Name = "Onyeka", Workplace = "Lafarge" }, new Employee { Name = "Canice", Workplace = "Lafarge" } });
                //v2.UpdateByCondition<Employee>(x => x.Id == 12, new Employee { Id = 1, Name = "Replacement", Workplace = "Google" });
                //v2.DeleteByCondition<Employee>(x => x.Id == 10);

                //v2.CreateTable<EmployeeV2>();
                //v2.InsertOne<EmployeeV2>(new EmployeeV2 { Id = Guid.Parse("b79856bc-a4a6-407e-952e-16ffe105ddb2"), Name = "Raighne", Workplace = "Google" });
                //v2.InsertMultiple<EmployeeV2>(new List<EmployeeV2> { new EmployeeV2 { Id = Guid.Parse("b79856bc-a4a6-407e-952e-16ffe105ddb3"), Name = "Onyeka", Workplace = "Google" }, new EmployeeV2 { Name = "Raighne", Workplace = "Google" } });
                //v2.UpdateByCondition<EmployeeV2>(x => x.Id == Guid.Parse("b79856bc-a4a6-407e-952e-16ffe105ddb2"), new EmployeeV2 { Name = "Replacement", Workplace = "Google" });
                //v2.DeleteOne<EmployeeV2>(Guid.Parse("b79856bc-a4a6-407e-952e-16ffe105ddb2"));

                //v2.CreateTable<EmployeeV4>();
                //v2.InsertOne<EmployeeV4>(new EmployeeV4 { Id = 3, Name = "Raighne", IsOld = false });
                //v2.InsertMultiple<EmployeeV3>(new List<EmployeeV3> { new EmployeeV3 { Id = "b79856bc-a4a6-407e-952e-16ffe105ddb3", Name = "Onyeka", Workplace = "Google" }, new EmployeeV3 { Name = "Raighne", Workplace = "Google" } });
                v2.UpdateByCondition<EmployeeV4>(x => x.Id == 3, new EmployeeV4 {Id = 3, Name = "Replacement"});
                //v2.DeleteOne<EmployeeV4>(3);


                //v2.InsertOne<EmployeeV3>(new EmployeeV3 {Id = "f58efbc7-b546-4d7f-9e80-d7bee606ef12" ,Name = "Sleep", Workplace = "Lafarge" });
                //v2.InsertOne<Employee>(new Employee { Name = "Canice", Workplace = "Lafarge" });

                //v2.InsertMultiple<Employee>(new List<Employee> { new Employee { Name = "Raighne", Workplace = "Lafarge" }, new Employee { Id = 10, Name = "Onyeka", Workplace = "Lafarge" }, new Employee { Name = "Canice", Workplace = "Lafarge" } });
                //v2.Commit();

                //v2.DeleteOne<Employee>(4);

                //v2.DeleteByCondition<EmployeeV4>(x => true);

                v2.Commit();

                //v2.DeleteOne<Employee>(3);
                //v2.Commit();
                foreach (var item in v2.FindAll<EmployeeV4>())
                {
                    Console.WriteLine($"{item.Id} {item.Name} {item.IsOld}");
                }

                Console.WriteLine(string.Join(',', v2.FindByCondition<Employee>(x => x.Id == 10 || x.Id == 11)));
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
                Console.ReadLine();
            }

        }
    }
}
