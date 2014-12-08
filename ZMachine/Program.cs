using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZMachine
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.BufferHeight = Console.WindowHeight;
            Console.WriteLine();
            var machine = Machine.LoadFromFile(args[0]);
            Console.Title = Path.GetFileName(args[0]);
            while (!machine.IsDone)
            {
                machine.Next();
            }
        }
    }
}
