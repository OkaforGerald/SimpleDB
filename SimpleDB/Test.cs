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
    }

    public class EmployeeV2
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public string Workplace { get; set; }
    }

    public class Test
    { 
        public static void Main(string[] args)
        {
            Database v1 = new Database("database.json");

            //v1.Insert<Employee>(new Employee { Id = 1, Name = "Onyeka", Workplace = "Lafarge" });

            //v1.Commit();

            Database v2 = new Database("v2.json");
            try
            {
                v2.Insert<EmployeeV2>(new EmployeeV2 { Id = Guid.Parse("569f6ece-2e0e-4bb9-a73d-22c0ffd93420"), Name = "Raighne", Workplace = "Google" });
                v2.Insert<EmployeeV2>(new EmployeeV2 { Id = Guid.Parse("569f6ece-2e0e-4bb9-a73d-22c0ffd93420"), Name = "Raighne", Workplace = "Google" });
                v2.Commit();

            }catch(Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
                Console.ReadLine();
            }
            
        }
    }
}
