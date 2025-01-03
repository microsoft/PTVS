using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Interpreter;

namespace EnvironmentDiscover
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var results = PythonRegistrySearch.PerformDefaultSearch();
            results.Where(r => r.Configuration.Version.Major == 3).ToList().ForEach(r => {
                Console.WriteLine($"3.{r.Configuration.Version.Minor}:{r.Configuration.Architecture} = '{r.Configuration.InterpreterPath}'");
            });
        }
    }
}
