using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using C = System.Console;

namespace ZMachine.Console
{
    class Program
    {
        private static string savesDir;

        static void Main(string[] args)
        {
            var machine = Machine.LoadFromFile(args[0]);
            savesDir = Path.GetDirectoryName(args[0]);

            machine.Output += OnOutput;
            machine.StatusLine += OnStatusLine;
            machine.BeginInput += OnInput;
            machine.SaveGame += OnSave;
            machine.RestoreGame += OnRestore;

            C.BufferHeight = C.WindowHeight;
            // Leave some space for the status line
            
            C.WriteLine();
            
            while (!machine.IsDone)
            {
                machine.Next();
            }
        }

        private static bool OnRestore(Machine machine)
        {
            C.Write("File name: ");
            var fname = C.ReadLine();
            var path = Path.Combine(savesDir, fname + ".zsave");
            return machine.LoadState(path);
        }

        private static bool OnSave(Machine machine)
        {
            C.Write("File name: ");
            var fname = C.ReadLine();
            var path = Path.Combine(savesDir, fname + ".zsave");
            if (File.Exists(path))
            {
                C.Write("File already exists. Overwrite existing file? (y/n)");
                if (C.ReadLine().Trim().ToLower() != "y")
                {
                    return false;
                }
            }

            return machine.SaveState(path);
        }

        private static void OnInput(Machine machine)
        {
            machine.EndInput(C.ReadLine());
        }

        private static void OnStatusLine(Machine machine, ZObject zObject, bool isScoreMoves, ushort score, ushort moves)
        {
            C.ForegroundColor = ConsoleColor.Black;
            C.BackgroundColor = ConsoleColor.DarkGray;
            var cursorTop = C.CursorTop;
            var cursorLeft = C.CursorLeft;

            C.SetCursorPosition(0, 0);
            C.Write(" " + zObject.GetName());

            string scoreDisplay;
            if (isScoreMoves)
            {
                scoreDisplay = string.Format("Score: {0}    Moves: {1} ", score, moves);
            }
            else
            {
                scoreDisplay = string.Format("Time: {0}:{1} ", score, moves);
            }

            for (var i = C.CursorLeft; i < C.WindowWidth - scoreDisplay.Length; i++)
            {
                C.Write(' ');
            }
            C.WriteLine(scoreDisplay);
            C.SetCursorPosition(cursorLeft, cursorTop);
            C.ResetColor();
        }

        private static void OnOutput(Machine machine, string text)
        {
            C.Write(text.Replace("\n", Environment.NewLine));
        }
    }
}
