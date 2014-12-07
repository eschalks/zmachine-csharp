using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZMachine
{
    [AttributeUsage(AttributeTargets.Method)]
    class OperationAttribute : Attribute
    {
        public string Name { get; private set; }
        public OperationType Type { get; private set; }
        public byte Num { get; private set; }

        public OperationAttribute(string name, OperationType type, byte num)
        {
            Name = name;
            Type = type;
            Num = num;
        }
    }
}
