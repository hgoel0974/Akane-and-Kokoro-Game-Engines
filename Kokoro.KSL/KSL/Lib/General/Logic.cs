﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if GLSL
using CodeGenerator = Kokoro.KSL.GLSL.GLSLCodeGenerator;
#if PC
using Kokoro.KSL.GLSL.PC;
#endif
#endif

namespace Kokoro.KSL.Lib.General
{
    public enum Comparisons
    {
        None = 0, LessThan = 1, GreaterThan = 2, Equal = 4, Not = 8, And = 16, Or = 32, GreaterThanEqual = 64, LessThanEqual = 128, Xor = 256
    }
    public class Comparison : Obj       //NOTE: DO something about this, it will work but it goes against the design, an Obj can be declared as a variable which makes this a weak point
    {
        public Obj Left;
        public Obj Right;
        public Comparisons Operation;

        public Comparison(Obj Left, Comparisons Op, Obj Right)
        {
            this.ObjName = CodeGenerator.TranslateCondition(Left, Op, Right);
            this.Left = Left;
            this.Right = Right;
            this.Operation = Op;
        }
    }

    /// <summary>
    /// Shading model. NOTE: These do not comply with the GPU Shading Model term, instead they are based off of GLSL version numbers
    /// </summary>
    public enum ShadingModel
    {
        SM1, SM2, SM3, SM4
    }

    public static class Logic
    {

        public static ShadingModel AvailableSM = ShadingModel.SM3;	//By default assume SM3 - GLSL 330 support

        private static string GenerateComparisonString(Comparison comparison)
        {
            return "";
        }

        public static void If(Comparison comparison, Action<dynamic> handler)
        {
            //Generate the code for the comparison and then call the handler after which this statement is closed
            SyntaxTree.Instructions.Enqueue(new SyntaxTree.Instruction()
            {
                instructionType = SyntaxTree.InstructionType.If,
                Parameters = new string[] { comparison.ObjName }
            });
            handler(Manager.VarDB);
            SyntaxTree.Instructions.Enqueue(new SyntaxTree.Instruction()
            {
                instructionType = SyntaxTree.InstructionType.EndIf
            });
        }

        public static void Else()
        {
            SyntaxTree.Instructions.Enqueue(new SyntaxTree.Instruction()
            {
                instructionType = SyntaxTree.InstructionType.Else
            });
        }

        internal static void UndoElse()
        {
            var tmp = new Queue<SyntaxTree.Instruction>(SyntaxTree.Instructions.Reverse());
            tmp.Dequeue();
            SyntaxTree.Instructions = new Queue<SyntaxTree.Instruction>(tmp.Reverse());
        }
    }
}
