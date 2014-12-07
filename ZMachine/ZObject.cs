using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZMachine
{
    class ZObject
    {
        public byte Id { get; private set; }
        private ObjectTable table;
        private uint address;

        public ZObject(ObjectTable table, byte id, uint address)
        {
            Id = id;
            this.table = table;
            this.address = address;
        }


        public bool IsAttribute(byte flag)
        {
            if (flag < 16)
            {
                var word = table.Machine.ReadWord(address);
                return BitUtil.IsBitSet(word, (byte)(15 - flag));
            }
            else
            {
                var word = table.Machine.ReadWord(address+2);
                return BitUtil.IsBitSet(word, (byte)(31 - flag));
            }
        }

        public void SetAttribute(byte flag, bool b)
        {
            if (!b)
                DebugAttrs();
            if (flag < 16)
            {
                var bit = 15 - flag;
                var word = table.Machine.ReadWord(address);
                var newWord = b ? (word | (1 << bit)) : (word & ~(1 << bit));
                table.Machine.WriteWord(address, (ushort)newWord);
            }
            else
            {
                var bit = 31 - flag;
                var word = table.Machine.ReadWord(address + 2);
                var newWord = b ? (word | (1 << bit)) : (word & ~(1 << bit));
                table.Machine.WriteWord(address+2, (ushort)newWord);
            }
            if (!b)
                DebugAttrs();
        }

        public string GetName()
        {
            var addr = GetPropertiesAddress(false);
            var textLength = table.Machine.ReadByte(addr);

            return table.Machine.ReadString(addr+1, (uint)(textLength*2));
        }

        uint GetPropertiesAddress(bool skipName)
        {
            uint addr = table.Machine.ReadWord(address + 7);
            if (!skipName)
                return addr;
            addr += (uint)(table.Machine.ReadByte(addr) * 2);
            return addr+1;
        }

        public void SetProperty(ushort property, ushort value)
        {
            table.Machine.WriteDebugLine("Setting property {0} of {1}", property, GetName());
            byte size;
            var addr = GetPropertyAddress(property, out size);

            if (size == 0)
            {
                throw new AccessViolationException("Trying to set non-existant property.");
            } 
            
            if (size == 1)
            {
                table.Machine.WriteByte(addr, (byte) (value & 0xFF));
            } else if (size == 2)
            {
                table.Machine.WriteWord(addr, value);
            }
            else
            {
                throw new NotImplementedException("Setting properties with more than 2 bytes.");
            }
        }

        public ushort GetProperty(ushort property)
        {
            byte size;
            var addr = GetPropertyAddress(property, out size);
            if (size == 0)
            {
                table.Machine.WriteDebugLine("Default value");
                return table.GetDefaultValue(property);
            }
            if (size > 2)
            {
                throw new AccessViolationException("Can't access large properties with this operation.");
            }
            return size == 1 ? table.Machine.ReadByte(addr) : table.Machine.ReadWord(addr);
        }

        public void Insert(ZObject obj)
        {
            obj.SetParent(this);
            var child = GetChild();
            if (child != null)
                obj.SetSibling(GetChild());

            SetChild(obj);

        }


        public ZObject GetParent()
        {
            return table.GetObject(table.Machine.ReadByte(address + 4));
        }

        public void SetParent(ZObject obj)
        {
            table.Machine.WriteByte(address+4, obj.Id);
        }

        public ZObject GetSibling()
        {
            return table.GetObject(table.Machine.ReadByte(address + 5));
        }

        public void SetSibling(ZObject obj)
        {
            table.Machine.WriteByte(address+5, obj.Id);
        }

        public ZObject GetChild()
        {
            return table.GetObject(table.Machine.ReadByte(address + 6));
        }

        public void SetChild(ZObject obj)
        {
            table.Machine.WriteByte(address+6, obj.Id);
        }


        public uint GetPropertyAddress(ushort idx, out byte propertySize)
        {
            var addr = GetPropertiesAddress(true);
            while (true)
            {
                var size = table.Machine.ReadByte(addr++);
                if (size == 0)
                {
                    propertySize = 0;
                    return 0;
                }
                var numBytes = (byte)((size >> 5) + 1);
                var propId = (ushort)(size & 0x1F);

                if (propId == idx)
                {
                    propertySize = numBytes;
                    return addr;
                }

                addr += numBytes;

            }
        }

        void DebugAttrs()
        {

//            Console.Write(GetName() + ": ");
//            for (byte i = 0; i < 32; i++)
//            {
//                if (IsAttribute(i))
//                {
//                    Console.Write("{0} ", i);
//                }
//            }
//            Console.WriteLine();
        }
    }
}
