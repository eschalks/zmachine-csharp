using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZMachine
{
    class ObjectTable
    {
        public Machine Machine { get; private set; }
        private readonly uint address;

        private readonly ZObject[] objects;

        public ObjectTable(Machine machine, uint address)
        {
            Machine = machine;
            this.address = address;
            objects = new ZObject[255];

            for (var i = 0; i < objects.Length; i++)
            {
                objects[i] = new ZObject(this, (byte)(i+1), (uint)(address + 62 + i * 9));
            }

            for (uint i = 0; i < 31; i++)
            {
                Machine.WriteDebugLine("Default value for {0} is {1} == {2}", i+1, Machine.ReadWord(address+i*2), GetDefaultValue((int)(i+1)));
            }
        }

        public ushort GetDefaultValue(int n)
        {
            n--;
            return Machine.ReadWord((uint)(address + n*2));
        }

        public ZObject GetObject(ushort id)
        {
            if (id == 0)
                return null;
            return objects[id-1];
        }

    }
}
