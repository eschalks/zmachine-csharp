using System;
using System.Collections.Generic;
using System.Text;

namespace ZMachine
{
    enum OperationType
    {
        Zero,
        One,
        Two,
        Var
    }

    partial class Machine
    {
        [Operation("je", OperationType.Two, 0x01)]

        void JumpEquals(ushort[] args)
        {
            var a = args[0];
            for (int i = 1; i < args.Length; i++)
            {
                if (a == args[i])
                {
                    Branch(true);
                    return;
                }
            }
            Branch(false);
        }

        [Operation("jl", OperationType.Two, 0x02)]
        void JumpLess(ushort[] args)
        {
            Branch((short)args[0] < (short)args[1]);
        }

        [Operation("jg", OperationType.Two, 0x03)]
        void JumpGreater(ushort[] args)
        {
            Branch((short)args[0] > (short)args[1]);
        }


        [Operation("dec_check", OperationType.Two, 0x04)]
        void DecrementCheck(ushort[] args)
        {
            var variable = (byte)args[0];
            var v = (short)ReadVariable(variable);
            v--;
            WriteVariable(variable, (ushort)v);
            Branch(v < (short)args[1]);
        }

        [Operation("inc_check", OperationType.Two, 0x05)]
        void IncrementCheck(ushort[] args)
        {
            var variable = (byte) args[0];
            var v = (short)ReadVariable(variable);
            v++;
            WriteVariable(variable,(ushort)v);
            Branch(v > (short)args[1]);
        }

        [Operation("jin", OperationType.Two, 0x06)]
        void JumpIfChild(ushort[] args)
        {
            var child = objectTable.GetObject(args[0]);
            Branch(child.GetParent().Id == args[1]);
        }

        [Operation("test", OperationType.Two, 0x07)]
        void Test(ushort[] args)
        {
            var bitmap = args[0];
            var flags = args[1];
            Branch((bitmap & flags) == flags);
        }

        [Operation("and", OperationType.Two, 0x09)]
        void And(ushort[] args)
        {
            Store(args[0] & args[1]);
        }

        [Operation("test_attr", OperationType.Two, 0x0A)]
        void TestAttribute(ushort[] args)
        {
            var attr = (byte) args[1];
            var obj = objectTable.GetObject(args[0]);
            if (attr > 255)
            {
                throw new NotImplementedException("Attributes that are indexed higher than a byte");
            }

            Branch(obj.IsAttribute(attr));
        }

        [Operation("set_attr", OperationType.Two, 0x0B)]
        void SetAttribute(ushort[] args)
        {
            var obj = objectTable.GetObject(args[0]);
            obj.SetAttribute((byte) args[1], true);
        }
        [Operation("clear_attr", OperationType.Two, 0x0C)]
        void ClearAttribute(ushort[] args)
        {
            var obj = objectTable.GetObject(args[0]);
            obj.SetAttribute((byte)args[1], false);
        }

        [Operation("store", OperationType.Two, 0x0D)]
        void StoreVariable(ushort[] args)
        {
            WriteVariable((byte)args[0], args[1]);
        }

        [Operation("insert_obj", OperationType.Two, 0x0E)]
        void InsertObject(ushort[] args)
        {
            var obj = objectTable.GetObject(args[0]);
            var newParent = objectTable.GetObject(args[1]);

            newParent.Insert(obj);
        }

        [Operation("loadw", OperationType.Two, 0x0F)]
        void LoadWord(ushort[] args)
        {
            Store(ReadWord((uint)(args[0]+args[1]*2)));
        }

        [Operation("loadb", OperationType.Two, 0x10)]
        void LoadByte(ushort[] args)
        {
            Store(ReadByte((uint)(args[0] + args[1])));
        }

        [Operation("get_prop", OperationType.Two, 0x011)]
        void GetProperty(ushort[] args)
        {
            var obj = objectTable.GetObject(args[0]);
            WriteDebugLine("Got property {1} of {0}", obj.GetName(), args[1]);
            Store(obj.GetProperty(args[1]));
        }

        [Operation("get_prop_addr", OperationType.Two, 0x12)]
        void GetPropertyAddress(ushort[] args)
        {
            var obj = objectTable.GetObject(args[0]);
            byte size;
            var addr = (ushort) obj.GetPropertyAddress(args[1], out size);
            Store(addr);
        }

        [Operation("add", OperationType.Two, 0x14)]
        void Add(ushort[] args)
        {
            Store((short)args[0]+(short)args[1]);
        }

        [Operation("sub", OperationType.Two, 0x15)]
        void Sub(ushort[] args)
        {
            Store((short)args[0] - (short)args[1]);
        }

        [Operation("mul", OperationType.Two, 0x16)]
        void Mul(ushort[] args)
        {
            Store((short)args[0] * (short)args[1]);
        }

        [Operation("div", OperationType.Two, 0x17)]
        void Div(ushort[] args)
        {
            Store((short)args[0] / (short)args[1]);
        }

        [Operation("jz", OperationType.One, 0x00)]
        void JumpZero(ushort a)
        {
            Branch(a == 0);
        }

        [Operation("get_sibling", OperationType.One, 0x01)]
        void GetSibling(ushort objId)
        {
            var sibling = objectTable.GetObject(objId).GetSibling();
            if (sibling == null)
            {
                Store(0);
                Branch(false);
            }
            else
            {
                Store(sibling.Id);
                Branch(true);
            }
        }

        [Operation("get_child", OperationType.One, 0x02)]
        void GetChild(ushort objId)
        {
            var child = objectTable.GetObject(objId).GetChild();
            if (child == null)
            {
                Store(0);
                Branch(false);
            }
            else
            {
                Store(child.Id);
                Branch(true);
            }
        }

        [Operation("get_parent", OperationType.One, 0x03)]
        void GetParent(ushort objId)
        {
            var parent = objectTable.GetObject(objId).GetParent();
            if (parent == null)
            {
                Store(0);
            }
            else
            {
                Store(objectTable.GetObject(objId).GetParent().Id);
            }
        }


        [Operation("get_prop_len", OperationType.One, 0x04)]
        void GetPropertyLength(ushort property)
        {
            if (property == 0)
            {
                Store(0);
                return;
            }

            var size = ReadByte((uint) (property - 1));
            var numBytes = (size >> 5) + 1;

            Store(numBytes);
        }

        [Operation("inc", OperationType.One, 0x05)]
        void Increment(ushort variable)
        {
            var v = (short)ReadVariable((byte)variable);
            v++;
            WriteVariable((byte)variable, (ushort)v);
        }

        [Operation("dec", OperationType.One, 0x06)]
        void Decrement(ushort variable)
        {
            var v = (short)ReadVariable((byte)variable);
            v--;
            WriteVariable((byte)variable, (ushort)v);
        }

        [Operation("print_addr", OperationType.One, 0x07)]
        void PrintAddress(ushort addr)
        {
            Console.Write(ReadString(addr));
        }

        [Operation("print_obj", OperationType.One, 0x0A)]
        void PrintObject(ushort objId)
        {
            var c = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(objectTable.GetObject(objId).GetName());
            Console.ForegroundColor = c;
        }

        [Operation("ret", OperationType.One, 0x0B)]
        void Return(ushort value)
        {
            var frame = callStack.Pop();

            while (stack.Count != frame.StackSize)
            {
                stack.Pop();
            }
            ProgramCounter = frame.ReturnPosition;
            WriteVariable(frame.ReturnStorage, value);
        }


        [Operation("jump", OperationType.One, 0x0C)]
        void Jump(ushort offset)
        {
            ProgramCounter = (uint)(ProgramCounter + (short)offset - 2);
        }

        [Operation("print_paddr", OperationType.One, 0x0D)]
        void PrintPackedAddress(ushort addr)
        {
            var paddr = GetPackedAddress(addr);

            if (paddr < startHighMemory)
                WriteDebugLine("WARNING: Packed address does not point to high memory.");
            Console.Write(ReadString(paddr));
        }

        [Operation("print", OperationType.Zero, 0x02)]
        void Print()
        {
            var str = ReadString();
            Console.Write(str);
        }

        [Operation("rtrue", OperationType.Zero, 0x00)]
        void ReturnTrue()
        {
            Return(1);
        }

        [Operation("rfalse", OperationType.Zero, 0x01)]
        void ReturnFalse()
        {
            Return(0);
        }

        [Operation("print_ret", OperationType.Zero, 0x03)]
        void PrintReturn()
        {
            Console.Write(ReadString());
            Return(1);
        }

        [Operation("ret_popped", OperationType.Zero, 0x08)]
        void ReturnPopped()
        {
            Return(ReadVariable(0));
            
        }

        [Operation("new_line", OperationType.Zero, 0x0B)]
        void NewLine()
        {
            Console.WriteLine();
        }

        [Operation("call", OperationType.Var, 0x00)]
        void Call(ushort[] args)
        {
            var address = GetPackedAddress(args[0]);
            var store = ReadByte();
            var returnTo = ProgramCounter;
            WriteDebugLine("call {0:X}", address);
            ProgramCounter = address;



            var variableCount = ReadByte();

            var frame = new CallFrame()
            {
                ReturnPosition = returnTo,
                ReturnStorage = store,
                Locals = new ushort[variableCount],
                Start = address,
                StackSize = stack.Count
            };

            for (var i = 0; i < frame.Locals.Length; i++)
            {
                frame.Locals[i] = ReadWord();
                if (args.Length > i + 1)
                {
                    frame.Locals[i] = args[i + 1];
                }
            }

            callStack.Push(frame);

            if (frame.Start == 0)
            {
                Return(0);
            }
        }

        [Operation("storew", OperationType.Var, 0x01)]
        void StoreWord(ushort[] args)
        {
            WriteWord((uint)(args[0] + args[1]*2), args[2]);
        }

        [Operation("storeb", OperationType.Var, 0x02)]
        private void StoreByte(ushort[] args)
        {
            WriteByte((uint) (args[0] + args[1]), (byte) args[2]);
        }

        [Operation("put_prop", OperationType.Var, 0x03)]
        void PutProperty(ushort[] args)
        {
            var obj = objectTable.GetObject(args[0]);
            obj.SetProperty(args[1], args[2]);
        }

        [Operation("read", OperationType.Var, 0x04)]
        void Read(ushort[] args)
        {
            var startColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            var input = Console.ReadLine() ?? "";
            Console.ForegroundColor = startColor;


            uint textBufferAddr = args[0];
            uint parseBufferAddr = args[1];

            var textBufferSize = ReadByte(textBufferAddr);
//            if (ReadByte(textBufferAddr + 1) > 0)
//            {
//                throw new NotImplementedException("Pre-existing text buffer content");
//            }

            input = input.Substring(0, Math.Min(input.Length, textBufferSize)).ToLower();
            var bytes = Encoding.ASCII.GetBytes(input);
            Array.Copy(bytes, 0, memory,textBufferAddr+1, bytes.Length);
            WriteByte((uint)(textBufferAddr+2+bytes.Length), 0);

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
                        wordStart = (byte)(i+1);
                    currentWord.Append(c); 
                }

                if ((c == ' ' || i == input.Length - 1) && currentWord.Length > 0)
                {
                    var addr = GetDictionaryEntryAddress(currentWord.ToString());
                    var blockStart = (uint)(parseBufferAddr + 2 + wordCount*4);
                    WriteWord(blockStart, addr);
                    WriteByte(blockStart+2, (byte)currentWord.Length);
                    WriteByte(blockStart+3, wordStart);

                    wordCount++;
                    currentWord.Clear();

                    if (wordCount == maxWords)
                        break;
                }

            }
            WriteByte(parseBufferAddr+1, wordCount);
        }

        [Operation("print_char", OperationType.Var, 0x05)]
        void PrintChar(ushort[] args)
        {
            Console.Write(EncodeChar(args[0]));
        }

        [Operation("print_num", OperationType.Var, 0x06)]
        void PrintNum(ushort[] args)
        {
            Console.Write((short)args[0]);
        }

        [Operation("random", OperationType.Var, 0x07)]
        void Random(ushort[] args)
        {
            if (args[0] > 0)
            {
                Store(random.Next(1, args[0]));
                return;
            }
            throw new NotImplementedException();
        }

        [Operation("push", OperationType.Var, 0x08)]
        void Push(ushort[] args)
        {
            WriteVariable(0, args[0]);
        }

        [Operation("pull", OperationType.Var, 0x09)]
        void Pull(ushort[] args)
        {
            var s = ReadVariable(0);
            if (args[0] > 255)
            {
                throw new AccessViolationException("Weird pull");
            }
            WriteVariable((byte)args[0], s);
        }
    }
}