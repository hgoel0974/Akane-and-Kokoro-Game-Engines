﻿#if OPENGL && PC

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kokoro.Engine.Shaders;
using Kokoro.Math;
using Kokoro.Engine;
using OpenTK.Graphics.OpenGL4;
using Kokoro.Sinus;

namespace Kokoro.OpenGL.PC
{
    public class ShaderProgramLL
    {
        private enum VarType
        {
            Matrix4, Matrix3, Matrix2, Vector4, Vector3, Vector2, Float, Texture, SSBO, UBO
        }

#if GL44        //TODO Finish updating ShaderProgramLL to use the AZDO model
        private static Dictionary<string, int> programDB = new Dictionary<string, int>();
        private string shaderName;
        private Shader[] shaderStages;
        private int id;

        public ShaderProgramLL(params Shader[] shaders)
        {
            variables = new Dictionary<string, shaderVars>();
            uniformLocations = new Dictionary<string, int>();
            shaderStages = new Shader[5];
            SinusManager.QueueCommand(() => id = GL.CreateProgram());
            shaderName = "";

            foreach (Shader s in shaders)
            {
                this.AttachShader(s);
            }

            SinusManager.QueueCommand(() =>
            {
                int uniformCount = 0;
                GL.GetProgram(id, GetProgramParameterName.ActiveUniforms, out uniformCount);
                for (int i = 0; i < uniformCount; i++)
                {
                    StringBuilder b = new StringBuilder(50);
                    int tmp = 0;
                    ActiveUniformType t = ActiveUniformType.Float;
                    GL.GetActiveUniform(id, i, 50, out tmp, out tmp, out t, b);

                    uniformLocations.Add(b.ToString(), i);
                }

            });
            //Wait for a moment so the commands can be processed?
            System.Threading.Thread.Sleep(1000);
        }

        public void AttachShader(Shader s)
        {

            if (s.GetShaderType() != ShaderTypes.TessellationControl) shaderStages[(int)s.GetShaderType()] = s;
            else
            {
                var shad = s as TessellationShader;
                shaderStages[(int)ShaderTypes.TessellationControl] = shad.control;
                shaderStages[(int)ShaderTypes.TessellationEval] = shad.eval;
            }

            //Build the shader name
            string name = "";
            for (int i = 0; i < 5; i++)
            {
                if (shaderStages[i] != null) name += shaderStages[i].GetID().ToString() + ",";
                else name += "-1,";
            }


            if (!programDB.ContainsKey(name))
            {
                if (!programDB.ContainsKey(shaderName)) //Backup current program
                {
                    programDB.Add(name, id);     //If the programDB doesn't contain the key, add the program
                }

                SinusManager.QueueCommand(() =>
                {
                    //Recreate the current program with all its shaders
                    //Link the program
                    id = GL.CreateProgram();

                    for (int i = 0; i < 5; i++)
                    {
                        if (shaderStages[i] != null) GL.AttachShader(id, shaderStages[i].GetID());
                    }
                    int result = 1;
                    GL.LinkProgram(id);
                    GL.GetProgram(id, GetProgramParameterName.LinkStatus, out result);
                    Kokoro.Debug.ErrorLogger.AddMessage(id, "Program Linking Result: " + result.ToString("X8") + "\n" + GL.GetProgramInfoLog(id), Kokoro.Debug.DebugType.Other, Kokoro.Debug.Severity.Notification);
                });
            }
            else id = programDB[name];                                       //Retrieve the shader if it already exists

            shaderName = name;  //Finally update the shader's name
        }

        #region Shader Handler

        private struct shaderVars
        {
            public int metadata;
            public int pos;
            public object obj;
            public VarType type;
        }
        //Using a dictionary with the name as the key helps prevent unnecessary entries in the variables list, old entries get overwritten automatically
        private Dictionary<string, shaderVars> variables;
        private Dictionary<string, int> uniformLocations;
        /// <summary>
        /// Set the shader and its parameters, should be called right before rendering to prevent any bugs
        /// </summary>
        /// <param name="context"></param>
        protected virtual void sApply(GraphicsContext context)
        {
            SinusManager.QueueCommand(() =>
            {
                List<shaderVars> variables = null;

                try
                {
                    variables = this.variables.Values.ToList();
                }catch(ArgumentException)
                {
                    //Stupid error thrown for no real reason, there aren't any arguments!!!
                    variables = this.variables.Values.ToList();
                }

                for (int i = 0; i < variables.Count; i++)
                {
                    switch (variables[i].type)
                    {
                        case VarType.Float:
                            GL.ProgramUniform1(id, variables[i].pos, (float)variables[i].obj);
                            break;
                        case VarType.Matrix2:
                            var mat2 = (Matrix2)variables[i].obj;
                            GL.ProgramUniformMatrix2(id, variables[i].pos, 1, false, (float[])mat2);
                            break;
                        case VarType.Matrix3:
                            var mat3 = (Matrix3)variables[i].obj;
                            GL.ProgramUniformMatrix3(id, variables[i].pos, 1, false, (float[])mat3);
                            break;
                        case VarType.Matrix4:
                            var mat4 = (Matrix4)variables[i].obj;
                            GL.ProgramUniformMatrix4(id, variables[i].pos, 1, false, (float[])mat4);
                            break;
                        case VarType.Texture:
                            (variables[i].obj as Texture).Bind(variables[i].metadata);
                            GL.ProgramUniform1(id, variables[i].pos, variables[i].metadata);
                            break;
                        case VarType.Vector2:
                            Vector2 tmpA = (Vector2)variables[i].obj;
                            GL.ProgramUniform2(id, variables[i].pos, 1, new float[] { tmpA.X, tmpA.Y });
                            break;
                        case VarType.Vector3:
                            Vector3 tmpB = (Vector3)variables[i].obj;
                            GL.ProgramUniform3(id, variables[i].pos, 1, new float[] { tmpB.X, tmpB.Y, tmpB.Z });
                            break;
                        case VarType.Vector4:
                            Vector4 tmpC = (Vector4)variables[i].obj;
                            GL.ProgramUniform4(id, variables[i].pos, 1, new float[] { tmpC.X, tmpC.Y, tmpC.Z, tmpC.W });
                            break;
                    }
                }

                GL.UseProgram(id);
            });
        }

        /// <summary>
        /// Clean up after the shader has been used (Apply + Draw)
        /// </summary>
        /// <param name="context">The current GraphicsContext</param>
        protected virtual void sCleanup(GraphicsContext context)
        {
            /*var variables = this.variables.Values.ToList();

            for (int i = 0; i < variables.Count; i++)
            {
                switch (variables[i].type)
                {
                    case VarType.Texture:
                        GL.ProgramUniform1(id, variables[i].pos, 0);
                        Texture.UnBind(variables[i].metadata);
                        break;
                    default:
                        break;
                }
            }
             * We shouldn't need any of these things with bindless uniforms
            */
            SinusManager.QueueCommand(() =>
            {
                for (int i = 0; i < 8; i++) Texture.UnBind(i);  //Unbind all textures

                GL.UseProgram(0);
            });
        }
        #endregion

        #region Shader Variables
        protected void aSetShaderBool(string name, bool val)
        {
            aSetShaderFloat(name, val ? 1 : 0);
        }
        protected void aSetShaderMatrix(string name, Matrix4 val)
        {
            if (uniformLocations.ContainsKey(name))
            {
                variables[name] = new shaderVars()
                {
                    obj = val,
                    pos = uniformLocations[name],
                    type = VarType.Matrix4
                };
            }
        }

        protected void aSetShaderMatrix(string name, Matrix2 val)
        {
            if (uniformLocations.ContainsKey(name))
            {
                variables[name] = new shaderVars()
                {
                    obj = val,
                    pos = uniformLocations[name],
                    type = VarType.Matrix2
                };
            }
        }

        protected void aSetShaderMatrix(string name, Matrix3 val)
        {
            if (uniformLocations.ContainsKey(name))
            {
                variables[name] = new shaderVars()
                {
                    obj = val,
                    pos = uniformLocations[name],
                    type = VarType.Matrix3
                };
            }
        }

        protected void aSetShaderVector(string name, Vector4 val)
        {
            if (uniformLocations.ContainsKey(name))
            {
                variables[name] = new shaderVars()
                {
                    obj = val,
                    pos = uniformLocations[name],
                    type = VarType.Vector4
                };
            }
        }

        protected void aSetShaderVector(string name, Vector3 val)
        {
            if (uniformLocations.ContainsKey(name))
            {
                variables[name] = new shaderVars()
                {
                    obj = val,
                    pos = uniformLocations[name],
                    type = VarType.Vector3
                };
            }
        }

        protected void aSetShaderVector(string name, Vector2 val)
        {
            if (uniformLocations.ContainsKey(name))
            {
                variables[name] = new shaderVars()
                {
                    obj = val,
                    pos = uniformLocations[name],
                    type = VarType.Vector2
                };
            }
        }

        protected void aSetShaderFloat(string name, float val)
        {
            if (uniformLocations.ContainsKey(name))
            {
                variables[name] = new shaderVars()
                {
                    obj = val,
                    pos = uniformLocations[name],
                    type = VarType.Float
                };
            }
        }

        int texUnit = 0;
        protected void aSetTexture(string name, Texture tex)
        {
            if (uniformLocations.ContainsKey(name))
            {
                if (variables.ContainsKey(name))
                {
                    variables[name] = new shaderVars()
                    {
                        metadata = variables[name].metadata,
                        obj = tex,
                        pos = variables[name].pos,
                        type = VarType.Texture
                    };
                }
                else
                {
                    variables[name] = new shaderVars()
                    {
                        metadata = texUnit++,
                        obj = tex,
                        pos = uniformLocations[name],
                        type = VarType.Texture
                    };
                }
            }
        }

        #endregion


        public void Dispose()
        {
            SinusManager.QueueCommand(() => GL.DeleteProgram(id));
        }
#else
        private static Dictionary<string, int> programDB = new Dictionary<string, int>();
        private string shaderName;
        private Shader[] shaderStages;
        private int id;

        public ShaderProgramLL(params Shader[] shaders)
        {
            variables = new Dictionary<string, shaderVars>();
            uniformLocations = new Dictionary<string, int>();
            shaderStages = new Shader[5];
            SinusManager.QueueCommand(() => id = GL.CreateProgram());
            shaderName = "";

            foreach (Shader s in shaders)
            {
                this.AttachShader(s);
            }

            SinusManager.QueueCommand(() =>
            {
                int uniformCount = 0;
                GL.GetProgram(id, GetProgramParameterName.ActiveUniforms, out uniformCount);
                for (int i = 0; i < uniformCount; i++)
                {
                    StringBuilder b = new StringBuilder(50);
                    int tmp = 0;
                    ActiveUniformType t = ActiveUniformType.Float;
                    GL.GetActiveUniform(id, i, 50, out tmp, out tmp, out t, b);

                    uniformLocations.Add(b.ToString(), i);
                }

            });
        }

        public void AttachShader(Shader s)
        {

            if (s.GetShaderType() != ShaderTypes.TessellationControl) shaderStages[(int)s.GetShaderType()] = s;
            else
            {
                var shad = s as TessellationShader;
                shaderStages[(int)ShaderTypes.TessellationControl] = shad.control;
                shaderStages[(int)ShaderTypes.TessellationEval] = shad.eval;
            }

            //Build the shader name
            string name = "";
            for (int i = 0; i < 5; i++)
            {
                if (shaderStages[i] != null) name += shaderStages[i].GetID().ToString() + ",";
                else name += "-1,";
            }


            if (!programDB.ContainsKey(name))
            {
                if (!programDB.ContainsKey(shaderName)) //Backup current program
                {
                    programDB.Add(name, id);     //If the programDB doesn't contain the key, add the program
                }

                SinusManager.QueueCommand(() =>
                {
                    //Recreate the current program with all its shaders
                    //Link the program
                    id = GL.CreateProgram();

                    for (int i = 0; i < 5; i++)
                    {
                        if (shaderStages[i] != null) GL.AttachShader(id, shaderStages[i].GetID());
                    }
                    int result = 1;
                    GL.LinkProgram(id);
                    GL.GetProgram(id, GetProgramParameterName.LinkStatus, out result);
                    Kokoro.Debug.ErrorLogger.AddMessage(id, "Program Linking Result: " + result.ToString("X8") + "\n" + GL.GetProgramInfoLog(id), Kokoro.Debug.DebugType.Other, Kokoro.Debug.Severity.Notification);
                });
            }
            else id = programDB[name];                                       //Retrieve the shader if it already exists

            shaderName = name;  //Finally update the shader's name
        }

        #region Shader Handler
        
        private struct shaderVars
        {
            public int metadata;
            public int pos;
            public object obj;
            public VarType type;
        }
        //Using a dictionary with the name as the key helps prevent unnecessary entries in the variables list, old entries get overwritten automatically
        private Dictionary<string, shaderVars> variables;
        private Dictionary<string, int> uniformLocations;
        /// <summary>
        /// Set the shader and its parameters, should be called right before rendering to prevent any bugs
        /// </summary>
        /// <param name="context"></param>
        protected virtual void sApply(GraphicsContext context)
        {
            SinusManager.QueueCommand(() =>
            {
                var variables = this.variables.Values.ToList();

                for (int i = 0; i < variables.Count; i++)
                {
                    switch (variables[i].type)
                    {
                        case VarType.Float:
                            GL.ProgramUniform1(id, variables[i].pos, (float)variables[i].obj);
                            break;
                        case VarType.Matrix2:
                            var mat2 = (Matrix2)variables[i].obj;
                            GL.ProgramUniformMatrix2(id, variables[i].pos, 1, false, (float[])mat2);
                            break;
                        case VarType.Matrix3:
                            var mat3 = (Matrix3)variables[i].obj;
                            GL.ProgramUniformMatrix3(id, variables[i].pos, 1, false, (float[])mat3);
                            break;
                        case VarType.Matrix4:
                            var mat4 = (Matrix4)variables[i].obj;
                            GL.ProgramUniformMatrix4(id, variables[i].pos, 1, false, (float[])mat4);
                            break;
                        case VarType.Texture:
                            (variables[i].obj as Texture).Bind(variables[i].metadata);
                            GL.ProgramUniform1(id, variables[i].pos, variables[i].metadata);
                            break;
                        case VarType.Vector2:
                            Vector2 tmpA = (Vector2)variables[i].obj;
                            GL.ProgramUniform2(id, variables[i].pos, 1, new float[] { tmpA.X, tmpA.Y });
                            break;
                        case VarType.Vector3:
                            Vector3 tmpB = (Vector3)variables[i].obj;
                            GL.ProgramUniform3(id, variables[i].pos, 1, new float[] { tmpB.X, tmpB.Y, tmpB.Z });
                            break;
                        case VarType.Vector4:
                            Vector4 tmpC = (Vector4)variables[i].obj;
                            GL.ProgramUniform4(id, variables[i].pos, 1, new float[] { tmpC.X, tmpC.Y, tmpC.Z, tmpC.W });
                            break;
                    }
                }

                GL.UseProgram(id);
            });
        }

        /// <summary>
        /// Clean up after the shader has been used (Apply + Draw)
        /// </summary>
        /// <param name="context">The current GraphicsContext</param>
        protected virtual void sCleanup(GraphicsContext context)
        {
            /*var variables = this.variables.Values.ToList();

            for (int i = 0; i < variables.Count; i++)
            {
                switch (variables[i].type)
                {
                    case VarType.Texture:
                        GL.ProgramUniform1(id, variables[i].pos, 0);
                        Texture.UnBind(variables[i].metadata);
                        break;
                    default:
                        break;
                }
            }
             * We shouldn't need any of these things with bindless uniforms
            */
            SinusManager.QueueCommand(() =>
            {
                for (int i = 0; i < 8; i++) Texture.UnBind(i);  //Unbind all textures

                GL.UseProgram(0);
            });
        }
        #endregion

        #region Shader Variables
        protected void aSetShaderBool(string name, bool val)
        {
            aSetShaderFloat(name, val ? 1 : 0);
        }
        protected void aSetShaderMatrix(string name, Matrix4 val)
        {
            variables[name] = new shaderVars()
            {
                obj = val,
                pos = uniformLocations[name],
                type = VarType.Matrix4
            };
        }

        protected void aSetShaderMatrix(string name, Matrix2 val)
        {
            variables[name] = new shaderVars()
            {
                obj = val,
                pos = uniformLocations[name],
                type = VarType.Matrix2
            };
        }

        protected void aSetShaderMatrix(string name, Matrix3 val)
        {
            variables[name] = new shaderVars()
            {
                obj = val,
                pos = uniformLocations[name],
                type = VarType.Matrix3
            };
        }

        protected void aSetShaderVector(string name, Vector4 val)
        {
            variables[name] = new shaderVars()
            {
                obj = val,
                pos = uniformLocations[name],
                type = VarType.Vector4
            };
        }

        protected void aSetShaderVector(string name, Vector3 val)
        {
            variables[name] = new shaderVars()
            {
                obj = val,
                pos = uniformLocations[name],
                type = VarType.Vector3
            };
        }

        protected void aSetShaderVector(string name, Vector2 val)
        {
            variables[name] = new shaderVars()
            {
                obj = val,
                pos = uniformLocations[name],
                type = VarType.Vector2
            };
        }

        protected void aSetShaderFloat(string name, float val)
        {
            variables[name] = new shaderVars()
            {
                obj = val,
                pos = uniformLocations[name],
                type = VarType.Float
            };
        }

        int texUnit = 0;
        protected void aSetTexture(string name, Texture tex)
        {
            if (variables.ContainsKey(name))
            {
                variables[name] = new shaderVars()
                {
                    metadata = variables[name].metadata,
                    obj = tex,
                    pos = variables[name].pos,
                    type = VarType.Texture
                };
            }
            else
            {
                variables[name] = new shaderVars()
                {
                    metadata = texUnit++,
                    obj = tex,
                    pos = uniformLocations[name],
                    type = VarType.Texture
                };
            }
        }
        #endregion


        public void Dispose()
        {
            SinusManager.QueueCommand(() => GL.DeleteProgram(id));
        }
#endif
    }
}

#endif