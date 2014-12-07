using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZMachine
{
    static class BitUtil
    {
        public static bool IsBitSet(ushort word, byte bit)
        {
            var bv = BitValue(word, bit);
            return bv > 0;
        }

        public static ushort BitValue(ushort word, byte bit)
        {
            return (ushort)(word & (1 << bit));
        }

        public static ushort BitValue(ushort word, byte bit, byte baseBit)
        {
            if (IsBitSet(word, bit))
            {
                return (ushort)(1 << (bit - baseBit));
            }
            return 0;
        }

        public static ushort MultiBitValue(ushort word, byte bit, byte count)
        {
            ushort total = 0;
            for (byte i = 0; i < count; i++)
            {
                total += BitValue(word, (byte)(bit + i), bit);
            }
            return total;
        }


    }
}
