﻿using System;
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
        public int? Id { get; set; }
        public string Name { get; set; }
        public bool IsOld { get; set; }
        public double number { get; set; }
        public decimal blah { get; set; }
        public long life { get; set; }
        public Guid guid { get; set; }
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
                //v2.InsertOne<EmployeeV4>(new EmployeeV4 { Name = "Raighne" });
                //v2.CreateTable<Employee>();

                v2.InsertOne<EmployeeV3>(new EmployeeV3 {Name = "Sleep", Workplace = "Lafarge" });
                //v2.InsertOne<Employee>(new Employee { Name = "Canice", Workplace = "Lafarge" });

                //v2.InsertMultiple<Employee>(new List<Employee> { new Employee { Name = "Raighne", Workplace = "Lafarge" }, new Employee { Id = 10, Name = "Onyeka", Workplace = "Lafarge" }, new Employee { Name = "Canice", Workplace = "Lafarge" } });
                //v2.Commit();

                //v2.DeleteOne<Employee>(4);

                //v2.DeleteByCondition<EmployeeV4>(x => true);

                v2.Commit();

                //v2.DeleteOne<Employee>(3);
                //v2.Commit();
                foreach (var item in v2.FindAll<Employee>())
                {
                    Console.WriteLine($"{item.Name} {item.Workplace}");
                }

                Console.WriteLine(v2.FindByCondition<Employee>(x => x.Id == 4 || x.Id == 24)
                    .FirstOrDefault()
                    .Name);
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
