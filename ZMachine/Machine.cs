﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ZMachine
{
    public partial class Machine
    {
        public byte Version { get; private set; }
        public bool IsDone { get; private set; }

        public delegate void OutputHandler(Machine machine, string text);
        public delegate void InputHandler(Machine machine);
        public delegate void StatusLineHandler(Machine machine, ZObject obj, bool isScoreMoves, ushort score, ushort moves);
        public delegate bool SaveHandler(Machine machine);


        public event OutputHandler Output;
        public event StatusLineHandler StatusLine;
        public event InputHandler BeginInput;
        public event SaveHandler SaveGame;
        public event SaveHandler RestoreGame;

        enum OperandType : byte
        {
            Large=0,
            Small=1,
            Variable=2,
            None=3
        }

        private byte[] memory;
        private byte[] initialMemory;
        private byte flags1;
        private ushort flags2;

        private readonly string[] ALPHABETS = new string[]
        {
            "abcdefghijklmnopqrstuvwxyz",
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
            " ^0123456789.,!?_#'\"/\\-:()"
        };


        private ushort startHighMemory;
        private ushort startDictionary;
        private ushort startObjects;
        private ushort startGlobals;
        private ushort startStatic;
        private ushort startAbbr;

        private Stack<CallFrame> callStack = new Stack<CallFrame>();
        private Stack<ushort> stack = new Stack<ushort>();
        private ushort[] readArgs;

        private ObjectTable objectTable;
        private Random random = new Random();

        public uint ProgramCounter { get; private set; }


        private Action[] OP0;
        private Action<ushort>[] OP1;
        private Action<ushort[]>[] OP2;
        private Action<ushort[]>[] VAR; 

        public Machine(byte[] data)
        {
            initialMemory = data;
            DetermineOperations();
            Initialize();
        }

        private void Initialize()
        {
            memory = (byte[])initialMemory.Clone();
            ParseHeader();
            objectTable = new ObjectTable(this, startObjects);
        }


        public static Machine LoadFromFile(string path)
        {
            return new Machine(File.ReadAllBytes(path));
        }

        public void Next()
        {
            if (readArgs != null)
            {
                throw new InvalidOperationException("Read in progress");
            }
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


        public void WriteDebugLine(string format, params object[] arg)
        {
            //Console.WriteLine(format, arg);
        }

        public void EndInput(string input)
        {
            uint textBufferAddr = readArgs[0];
            uint parseBufferAddr = readArgs[1];
            readArgs = null;

            var textBufferSize = ReadByte(textBufferAddr);
//            if (ReadByte(textBufferAddr + 1) > 0)
//            {
//                throw new NotImplementedException("Pre-existing text buffer content");
//            }

            input = input.Substring(0, Math.Min(input.Length, textBufferSize)).ToLower();
            var bytes = Encoding.ASCII.GetBytes(input);
            Array.Copy(bytes, 0, memory, textBufferAddr + 1, bytes.Length);
            WriteByte((uint) (textBufferAddr + 2 + bytes.Length), 0);

            // TODO: get dictionary word separators; found in dictionary, are included into parse buffer (as opposed to spaces)

            var maxWords = ReadByte(parseBufferAddr);
            var currentWord = new StringBuilder();
            byte wordCount = 0;
            byte wordStart = 0;
            for (byte i = 0; i < input.Length; i++)
            {
                var c = input[i];
                if (c != ' ')
                {
                    if (currentWord.Length == 0)
                        wordStart = (byte) (i + 1);
                    currentWord.Append(c);
                }

                if ((c == ' ' || i == input.Length - 1) && currentWord.Length > 0)
                {
                    var addr = GetDictionaryEntryAddress(currentWord.ToString());
                    var blockStart = (uint) (parseBufferAddr + 2 + wordCount*4);
                    WriteWord(blockStart, addr);
                    WriteByte(blockStart + 2, (byte) currentWord.Length);
                    WriteByte(blockStart + 3, wordStart);

                    wordCount++;
                    currentWord.Clear();

                    if (wordCount == maxWords)
                        break;
                }

            }
            WriteByte(parseBufferAddr + 1, wordCount);
        }

        void Write(string text)
        {
            if (Output != null)
            {
                Output(this, text);
            }
        }

        void WriteLine(string text)
        {
            Write(text+'\n');
        }

        public bool SaveState(string path)
        {                          
            using (var stream = new FileStream(path, FileMode.Create))
                {
                    using (var writer = new BinaryWriter(stream))
                    {
                        // Write dynamic memory
                        writer.Write(memory, 0, startStatic);

                        // Write program counter
                        writer.Write(ProgramCounter);

                        // Write stack
                        var stackList = stack.ToList();
                        writer.Write(stackList.Count);
                        stackList.Reverse();

                        foreach (var v in stackList)
                        {
                            writer.Write(v);
                        }

                        // Write call stack
                        var callList = callStack.ToList();
                        writer.Write(callList.Count);
                        callList.Reverse();

                        foreach (var callFrame in callList)
                        {
                            writer.Write(callFrame.Start);
                            writer.Write(callFrame.StackSize);
                            writer.Write(callFrame.ReturnPosition);
                            writer.Write(callFrame.ReturnStorage);
                            writer.Write(callFrame.Locals.Length);

                            foreach (var local in callFrame.Locals)
                            {
                                writer.Write(local);
                            }
                        }
                    }

                    return true;
                }

        }

        public bool LoadState(string path)
        {           
            using (var stream = new FileStream(path, FileMode.Open))
            {
                using (var reader = new BinaryReader(stream))
                {
                    // Read header
                    var header = new byte[32];
                    if (reader.Read(header, 0, header.Length) != header.Length)
                    {
                        return false;
                    }

                    // Validate checksum
                    if (memory[0x1C] != header[0x1C] || memory[0x1D] != header[0x1D])
                    {
                        return false;
                    }


                    var staticMemoryBase = header[0xE] * (1 << 8) + header[0xF];
                    stream.Seek(0, SeekOrigin.Begin);

                    // From here on out any error should just be an exception since we're modifying memory now
                    if (reader.Read(memory, 0, staticMemoryBase) != staticMemoryBase)
                    {
                        throw new InvalidDataException("Invalid save file");
                    }

                    ProgramCounter = reader.ReadUInt32();

                    var stackSize = reader.ReadInt32();
                    stack.Clear();
                    for (var i = 0; i < stackSize; i++)
                    {
                        stack.Push(reader.ReadUInt16());
                    }

                    stackSize = reader.ReadInt32();
                    callStack.Clear();
                    for (var i = 0; i < stackSize; i++)
                    {
                        var callFrame = new CallFrame()
                        {
                            Start = reader.ReadUInt32(),
                            StackSize = reader.ReadInt32(),
                            ReturnPosition = reader.ReadUInt32(),
                            ReturnStorage = reader.ReadByte(),
                            Locals = new ushort[reader.ReadInt32()]
                        };

                        for (int j = 0; j < callFrame.Locals.Length; j++)
                        {
                            callFrame.Locals[j] = reader.ReadUInt16();
                        }

                        callStack.Push(callFrame);
                    }

                }

            }
            return true;

        }
    }
}
