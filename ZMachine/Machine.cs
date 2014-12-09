using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ZMachine
{
    partial class Machine
    {
        public byte Version { get; private set; }
        public bool IsDone { get; private set; }

        enum OperandType : byte
        {
            Large=0,
            Small=1,
            Variable=2,
            None=3
        }

        private byte[] memory;
        private byte flags1;
        private ushort flags2;

        private readonly string[] ALPHABETS = new string[]
        {
            "abcdefghijklmnopqrstuvwxyz",
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
            " ^0123456789.,!?_#'\"/\\-:()"
        };

        private string savesDir;

        private ushort startHighMemory;
        private ushort startDictionary;
        private ushort startObjects;
        private ushort startGlobals;
        private ushort startStatic;
        private ushort startAbbr;

        private Stack<CallFrame> callStack = new Stack<CallFrame>();
        private Stack<ushort> stack = new Stack<ushort>();

        private ObjectTable objectTable;
        private Random random = new Random();

        public uint ProgramCounter { get; private set; }


        private Action[] OP0;
        private Action<ushort>[] OP1;
        private Action<ushort[]>[] OP2;
        private Action<ushort[]>[] VAR; 

        public Machine(byte[] data, string savesDir)
        {
            memory = data;
            this.savesDir = savesDir;
            ParseHeader();
            DetermineOperations();
            objectTable = new ObjectTable(this, startObjects);
        }


        public static Machine LoadFromFile(string path)
        {
            return new Machine(File.ReadAllBytes(path), Path.GetDirectoryName(path));
        }

        public void Next()
        {
            var pc = ProgramCounter;
            var opcode = ReadByte();
            var form = BitUtil.MultiBitValue(opcode, 6, 2);
            DebugOperation(pc, opcode, form);
            if (form == 3)
            {
                var operandTypes = new OperandType[4];
                var typeByte = ReadByte();

                for (var i = 0; i < operandTypes.Length; i++)
                {
                    operandTypes[3 - i] = (OperandType)BitUtil.MultiBitValue(typeByte, (byte)(i*2), 2);
                }

                var num = BitUtil.MultiBitValue(opcode, 0, 5);

                var f = BitUtil.IsBitSet(opcode, 5) ? VAR[num] : OP2[num];

                if (f == null)
                {
                    if (BitUtil.IsBitSet(opcode, 5))
                    {
                        throw new NotImplementedException(string.Format("VAR:{0:X} @ {1:X}", num, pc));
                    }
                    throw new NotImplementedException(string.Format("2OP:{0:X}", num));
                }
                f(ReadOperands(operandTypes));
            } else if (form == 2)
            {
                var type = (OperandType) BitUtil.MultiBitValue(opcode, 4, 2);
                var num = BitUtil.MultiBitValue(opcode, 0, 4);
                if (type == OperandType.None)
                {
                    var f = OP0[num];
                    if (f == null)
                    {
                        throw new NotImplementedException(string.Format("0OP:{0:X} @ {1:X}", num, pc));
                    }
                    f();
                }
                else
                {
                    var f = OP1[num];
                    if (f == null)
                    {
                        throw new NotImplementedException(string.Format("1OP:{0:X} @ {1:X}", num, pc));
                    }
                    f(ReadOperand(type).Value);
                }
            }
            else
            {
                var num = BitUtil.MultiBitValue(opcode, 0, 5);
                var f = OP2[num];
                if (f == null)
                {
                    throw new NotImplementedException(string.Format("2OP:{0:X} @ {1:X}", num, pc));
                }

                var op1 = BitUtil.IsBitSet(opcode, 6)
                    ? ReadOperand(OperandType.Variable)
                    : ReadOperand(OperandType.Small);

                var op2 = BitUtil.IsBitSet(opcode, 5)
                    ? ReadOperand(OperandType.Variable)
                    : ReadOperand(OperandType.Small);

                OP2[num](new[] {op1.Value, op2.Value});
            }
        }

        private void ParseHeader()
        {
            Version = ReadByte(0x00);
            if (Version != 3)
            {
                throw new InvalidOperationException("Only Version 3 files are supported.");
            }
            flags1 = ReadByte(0x01);
            startHighMemory = ReadWord(0x04);
            ProgramCounter = ReadWord(0x06);
            startDictionary = ReadWord(0x08);
            startObjects = ReadWord(0x0A);
            startGlobals = ReadWord(0x0C);
            startStatic = ReadWord(0x0E);
            flags2 = ReadWord(0x10);
            startAbbr = ReadWord(0x18);

        }

        char[] GetWordSeparators()
        {
            throw new NotImplementedException();
        }

        private ushort GetDictionaryEntryAddress(string word)
        {
            word = word.Substring(0, Math.Min(6, word.Length));
            uint pos = startDictionary;
            var n = ReadByte(pos++);
            pos += n;
            var entryLength = ReadByte(pos++);
            var entryCount = ReadWord(pos);
            pos += 2;

            for (uint i = 0; i < entryCount; i++)
            {
                var addr = pos + i*entryLength;
                var str = ReadString(addr);
                if (str == word)
                    return (ushort)addr;
            }
            return 0;
        }

//        private string[] ParseDictionary()
//        {
//            uint pos = startDictionary;
//            var n = ReadByte(pos++);
//            pos += n;
//            var entryLength = ReadByte(pos++);
//            var entryCount = ReadWord(pos);
//            pos += 2;
//            var result = new string[entryCount];
//
//            for (uint i = 0; i < entryCount; i++)
//            {
//                var addr = pos + i*entryLength;
//                result[i] = ReadString(addr, )
//               
//            }
//        }

        private void DetermineOperations()
        {
            OP0 = new Action[16];
            OP1 = new Action<ushort>[16];
            OP2 = new Action<ushort[]>[32];
            VAR = new Action<ushort[]>[32];

            var methods = GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var method in methods)
            {
                var attrs = method.GetCustomAttributes(typeof(OperationAttribute), false);
                if (attrs.Length > 0)
                {
                    var attr = attrs[0] as OperationAttribute;
                    if (attr != null)
                    {
                        if (attr.Type == OperationType.Var)
                        {
                            VAR[attr.Num] = (Action<ushort[]>)method.CreateDelegate(typeof (Action<ushort[]>), this);
                        } else if (attr.Type == OperationType.Two)
                        {
                            OP2[attr.Num] = (Action<ushort[]>)method.CreateDelegate(typeof(Action<ushort[]>), this);
                        } else if (attr.Type == OperationType.One)
                        {
                            OP1[attr.Num] = (Action<ushort>) method.CreateDelegate(typeof (Action<ushort>), this);
                        }
                        else
                        {
                            OP0[attr.Num] = (Action)method.CreateDelegate(typeof(Action), this);
                        }
                    }
                }
            }

        }

        private byte ReadByte()
        {
            return memory[ProgramCounter++];
        }

        public byte ReadByte(uint index)
        {
            return memory[index];
        }

        private ushort ReadWord()
        {
            ProgramCounter += 2;
            return ReadWord(ProgramCounter-2);
        }

        public ushort ReadWord(uint index)
        {
            return (ushort)(memory[index + 1] + memory[index]*(1 << 8));
        }


        public void WriteWord(uint index, ushort word)
        {
            memory[index] = (byte)(word >> 8);
            memory[index + 1] = (byte) (word & 0xFF);
        }

        public void WriteByte(uint index, byte b)
        {
            memory[index] = b;
        }

        private ushort? ReadOperand(OperandType type)
        {
            if (type == OperandType.Small)
            {
                return ReadByte();
            }
            if (type == OperandType.Large)
            {
                return ReadWord();
            }

            if (type == OperandType.Variable)
            {
                return ReadVariable(ReadByte());
            }

            return null;
        }

        public string ReadString()
        {
            uint pc;
            var str = ReadString(ProgramCounter, uint.MaxValue-1, out pc);
            ProgramCounter = pc;
            return str;
        }

        public string ReadString(uint address)
        {
            return ReadString(address, uint.MaxValue-1);
        }

        public string ReadString(uint address, uint maxBytes)
        {
            uint i;
            return ReadString(address, maxBytes, out i);
        }

        public string ReadString(uint address, out uint endAddress)
        {
            return ReadString(address, uint.MaxValue - 1, out endAddress);
        }


        public string ReadString(uint address, uint maxBytes, out uint endAddress)
        {
            if (maxBytes%2 != 0)
            {
                throw new NotImplementedException("Odd-sized strings");
            }

            var result = new StringBuilder();
            endAddress = address;
            int alphabet = 0;
            int abbr = 0;
            var maxWords = maxBytes/2;
            bool tenBuild = false;
            int tenBase = -1;
            for (var i = 0; i < maxWords; i++)
            {
                var word = ReadWord(endAddress);
                endAddress += 2;
                var isLast = BitUtil.IsBitSet(word, 15);

                for (var j = 2; j >= 0; j--)
                {
                    var chr = BitUtil.MultiBitValue(word, (byte)(j*5), 5);

                    if (tenBuild)
                    {
                        if (tenBase >= 0)
                        {
                            chr = (ushort)(tenBase*(1<<5) + chr);
                            tenBase = -1;
                            tenBuild = false;
                            result.Append(EncodeChar(chr));
                        }
                        else
                        {
                            tenBase = chr;
                        }
                    }
                    else if (abbr == 0)
                    {
                        if (chr == 4)
                        {
                            alphabet = 1;
                        }
                        else if (chr == 5)
                        {
                            alphabet = 2;
                        }
                        else
                        {
                            if (chr == 0)
                            {
                                result.Append(' ');
                            }
                            else if (chr == 1 || chr == 2 || chr == 3)
                            {
                                abbr = chr;
                            }
                            else if (chr == 6 && alphabet == 2)
                            {
                                tenBuild = true;
                            }
                            else if (chr == 7 && alphabet == 2)
                            {
                                result.AppendLine();
                            }
                            else
                            {
                                result.Append(ALPHABETS[alphabet][chr - 6]);
                            }

                            alphabet = 0;
                        }
                    }
                    else
                    {
                        result.Append(GetAbbreviation((uint)(32*(abbr - 1) + chr)));
//                        alphabet = 0;
                        abbr = 0;
                    }

                }

                if (isLast)
                {
                    break;
                }
            }

            return result.ToString();
        }

        private char EncodeChar(ushort code)
        {
            if (code >= 32 && code <= 126)
            {
                return (char)code;
            }
            throw new NotImplementedException("Unsupported ZSCII code");
        }

        private string GetAbbreviation(uint index)
        {
            uint addr = ReadWord(startAbbr + index*2);
            return ReadString(addr*2);
        }

        private ushort ReadVariable(byte variable)
        {
            if (variable == 0)
            {
                var frame = callStack.FirstOrDefault();
                if (frame != null)
                {
                    if (stack.Count == frame.StackSize)
                    {
                        throw new AccessViolationException("Invalid stack access");
                    }
                }
                return stack.Pop();
            }
            if (variable > 0 && variable < 0x0F)
            {
                var frame = callStack.FirstOrDefault();
                if (frame == null)
                {
                    throw new InvalidOperationException("Can't access local variables outisde of a routine.");
                }
                if (variable > frame.Locals.Length)
                {
                    throw new AccessViolationException("Trying to access non-existant local variable.");
                }
                return frame.Locals[variable - 1];
            }
            if (variable >= 0x10)
            {
                return ReadWord((uint)(startGlobals + (variable-0x10)*2));
            }
            throw new AccessViolationException("Incorrect variable address.");
        }

        private void WriteVariable(byte variable, ushort value)
        {
            if (variable == 0)
            {
                stack.Push(value);
            }
            else if (variable > 0 && variable < 0x0F)
            {
                var frame = callStack.FirstOrDefault();
                if (frame == null)
                {
                    throw new InvalidOperationException("Can't access local variables outisde of a routine.");
                }
                if (variable > frame.Locals.Length)
                {
                    throw new AccessViolationException("Trying to access non-existant local variable.");
                }
                frame.Locals[variable - 1] = value;
            }
            else if (variable >= 0x10)
            {
                WriteWord((uint)(startGlobals + (variable - 0x10) * 2), value);
            }
            else
            {
                throw new AccessViolationException("Incorrect variable address.");
            }
        }

        private ushort[] ReadOperands(ICollection<OperandType> types)
        {
            var res = new List<ushort>(types.Count);
            foreach (var type in types)
            {
                var op = ReadOperand(type);
                if (!op.HasValue)
                {
                    break;
                }
                res.Add(op.Value);
            }
            return res.ToArray();
        }

        public uint GetPackedAddress(ushort address)
        {
            return (uint)(address*2);
        }

        private void Store(ushort value)
        {
            var variable = ReadByte();
            WriteVariable(variable, value);
        }

        private void Store(int value)
        {
            Store((ushort)value);
        }

        private void Branch(bool condition)
        {
            var branch = ReadByte();
            if (condition == BitUtil.IsBitSet(branch, 7))
            {
                ushort offset;
                if (BitUtil.IsBitSet(branch, 6))
                {
                    offset = BitUtil.MultiBitValue(branch, 0, 6);
                }
                else
                {
                    var word = ReadWord(ProgramCounter-1);
                    ReadByte();
                    offset = BitUtil.MultiBitValue(word, 0, 14);
                    if (BitUtil.IsBitSet(word, 13))
                    {
                        offset |= (1 << 14) | (1 << 15);
                    }
                    WriteDebugLine("Jump to {0:X}", ProgramCounter+(short)offset-2);
//                    throw new NotImplementedException("Two byte branches");
                }
                if (offset == 0 || offset == 1)
                {
                    Return((ushort)offset);
                }
                else
                {
                    ProgramCounter += (uint) ((short)offset - 2);
                }
            } else if (!BitUtil.IsBitSet(branch, 6))
            {
                ReadByte();
            }
        }

        void DebugOperation(uint pc, byte opcode, ushort form)
        {
            var num = form == 2 ? BitUtil.MultiBitValue(opcode, 0, 4) : BitUtil.MultiBitValue(opcode, 0, 5);
            WriteDebugLine("[{0:X}] {1:X}", pc, num);
        }

        public void WriteStatusLine()
        {
            var obj = objectTable.GetObject(ReadWord(startGlobals));
            var score = ReadWord((uint)(startGlobals + 2));
            var moves = ReadWord((uint)(startGlobals + 4));
//            if ((flags1 & 0x02) == 0x02)
//            {
//                Console.Title = string.Format("{0} {1}:{2}", obj.GetName(), score, moves);
//            }
//            else
//            {
//                Console.Title = string.Format("{0} {1}/{2}", obj.GetName(), score, moves);
//            }
            var c = Console.ForegroundColor;
            var b = Console.BackgroundColor;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.BackgroundColor = ConsoleColor.DarkGray;
            var cursorTop = Console.CursorTop;
            var cursorLeft = Console.CursorLeft;
            //Console.SetCursorPosition(0, cursorTop-1);
//            Console.SetCursorPosition(0, Math.Max(0, cursorTop-Console.WindowHeight));
            Console.SetCursorPosition(0,0);
            Console.Write(" " + obj.GetName());

            string scoreDisplay;
            if ((flags1 & 0x02) == 0x02)
            {
                scoreDisplay = string.Format("Time: {0}:{1} ", score, moves); 
            }
            else
            {
                scoreDisplay = string.Format("Score: {0}    Moves: {1} ", score, moves); 
            }

            for (var i = Console.CursorLeft; i < Console.WindowWidth - scoreDisplay.Length; i++)
            {
                Console.Write(' ');
            }
            Console.WriteLine(scoreDisplay);

            Console.SetCursorPosition(cursorLeft, cursorTop);

            Console.ForegroundColor = c;
            Console.BackgroundColor = b;
        }

        public void WriteDebugLine(string format, params object[] arg)
        {
            //Console.WriteLine(format, arg);
        }
    }
}
