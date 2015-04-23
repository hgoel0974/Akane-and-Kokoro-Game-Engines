﻿using Kokoro.Engine.Shaders;
using Kokoro.KSL;
using Kokoro.KSL.Lib;
using Kokoro.KSL.Lib.Math;
using Kokoro.KSL.Lib.Texture;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kokoro.ShaderLib
{
    public class BlendShader : IKUbershader
    {
        Kokoro.Engine.BlendingFactor src, dst;

        public BlendShader(Kokoro.Engine.BlendingFactor s = 0, Kokoro.Engine.BlendingFactor d = 0)
        {
            src = s;
            dst = d;
        }

        public void Fragment(int num)
        {
            var Vars = Manager.ShaderStart("Bloom_S_Frag");
            Manager.SharedIn<Vec2>("UV");
            Manager.Uniform<Sampler2D>("SourceA");
            Manager.Uniform<Sampler2D>("SourceB");

            Manager.StreamOut<Vec4>("Color", 0);

            Manager.Create<Vec4>("SrcA");
            Manager.Create<Vec4>("SrcB");

            Vars.SrcA = Texture.Read2D(Vars.SourceA, Vars.UV);
            Vars.SrcB = Texture.Read2D(Vars.SourceB, Vars.UV);

            switch (src)
            {
                case Engine.BlendingFactor.ConstantAlpha:
                    Manager.Uniform<KFloat>("weightSrcA");
                    break;
                case Engine.BlendingFactor.DstAlpha:
                    Manager.Create<KFloat>("weightSrcA");
                    Vars.weightSrcA = Vars.SrcB["a"];
                    break;
                case Engine.BlendingFactor.SrcAlpha:
                    Manager.Create<KFloat>("weightSrcA");
                    Vars.weightSrcA = Vars.SrcA["a"];
                    break;
                case Engine.BlendingFactor.One:
                    Manager.Create<KFloat>("weightSrcA");
                    Vars.weightSrcA = 1;
                    break;
                case Engine.BlendingFactor.Zero:
                    Manager.Create<KFloat>("weightSrcA");
                    Vars.weightSrcA = 0;
                    break;
                case Engine.BlendingFactor.OneMinusSrcAlpha:
                    Manager.Create<KFloat>("weightSrcA");
                    Vars.weightSrcA = 1.0f - Vars.SrcA["a"];
                    break;
                case Engine.BlendingFactor.OneMinusDstAlpha:
                    Manager.Create<KFloat>("weightSrcA");
                    Vars.weightSrcA = 1.0f - Vars.SrcB["a"];
                    break;
            }

            switch (dst)
            {
                case Engine.BlendingFactor.ConstantAlpha:
                    Manager.Uniform<KFloat>("weightSrcB");
                    break;
                case Engine.BlendingFactor.DstAlpha:
                    Manager.Create<KFloat>("weightSrcB");
                    Vars.weightSrcB = Vars.SrcB["a"];
                    break;
                case Engine.BlendingFactor.SrcAlpha:
                    Manager.Create<KFloat>("weightSrcB");
                    Vars.weightSrcB = Vars.SrcA["a"];
                    break;
                case Engine.BlendingFactor.One:
                    Manager.Create<KFloat>("weightSrcB");
                    Vars.weightSrcB = 1;
                    break;
                case Engine.BlendingFactor.Zero:
                    Manager.Create<KFloat>("weightSrcB");
                    Vars.weightSrcB = 0;
                    break;
                case Engine.BlendingFactor.OneMinusSrcAlpha:
                    Manager.Create<KFloat>("weightSrcB");
                    Vars.weightSrcB = 1.0f - Vars.SrcA["a"];
                    break;
                case Engine.BlendingFactor.OneMinusDstAlpha:
                    Manager.Create<KFloat>("weightSrcB");
                    Vars.weightSrcB = 1.0f - Vars.SrcB["a"];
                    break;
            }

            Vars.Color = Vars.SrcA * Vars.weightSrcA + Vars.SrcB * Vars.weightSrcB;
        }

        public void Vertex()
        {
            var Vars = Manager.ShaderStart("Bloom_S_Vert");
            Manager.StreamIn<Vec3>("VertexPos", 0);
            Manager.StreamIn<Vec2>("UV0", 2);

            Manager.SharedOut<Vec2>("UV");

            Vars.VertexPosition.Construct(Vars.VertexPos, 1);
            Vars.UV = Vars.UV0;
            Manager.ShaderEnd();
        }

        public static Ubershader Create(Kokoro.Engine.BlendingFactor s = 0, Kokoro.Engine.BlendingFactor d = 0)
        {
            return new Ubershader(new BlendShader(s, d));
        }

        public static object Create(ShaderTypes s, Kokoro.Engine.BlendingFactor src = 0, Kokoro.Engine.BlendingFactor d = 0)
        {
            if (s == ShaderTypes.Vertex) return (Action)new BlendShader(src, d).Vertex;
            else if (s == ShaderTypes.Fragment) return (Action<int>)new BlendShader(src, d).Fragment;

            return null;
        }
    }
}