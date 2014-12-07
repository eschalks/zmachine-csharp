using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZMachine
{
    class CallFrame
    {
        public uint Start;
        public ushort[] Locals;
        public uint ReturnPosition;
        public byte ReturnStorage;
        public int StackSize;
    }
}
