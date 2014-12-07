using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZMachine
{
    class Program
    {
        static void Main(string[] args)
        {
            var machine = Machine.LoadFromFile(args[0]);
            while (true)
            {
                machine.Next();
            }
        }
    }
}
