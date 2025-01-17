﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

namespace Kokoro.Engine.Shaders
{
    public class TessellationShader : Shader
    {
        internal TessellationEvalShader eval;
        internal TessellationControlShader control;

        public void SetPatchSize(int num)
        {
            base.aSetPatchSize(num);
        }

        public TessellationShader(string controlShader, string evalShader) : base("", ShaderTypes.TessellationComb)
        {
            eval = new TessellationEvalShader(evalShader);
            control = new TessellationControlShader(controlShader);
            SetPatchSize(3);
        }
    }

    class TessellationControlShader : Shader
    {
        public TessellationControlShader(string fshader) : base(fshader, ShaderTypes.TessellationControl) { }

    }

    class TessellationEvalShader : Shader
    {
        public TessellationEvalShader(string fshader) : base(fshader, ShaderTypes.TessellationEval) { }
    }
}
