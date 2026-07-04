/// <copyright>
///
/// SharpQuakeEvolved changes by optimus-code, 2019
/// 
/// Based on SharpQuake (Quake Rewritten in C# by Yury Kiselev, 2010.)
///
/// Copyright (C) 1996-1997 Id Software, Inc.
///
/// This program is free software; you can redistribute it and/or
/// modify it under the terms of the GNU General Public License
/// as published by the Free Software Foundation; either version 2
/// of the License, or (at your option) any later version.
///
/// This program is distributed in the hope that it will be useful,
/// but WITHOUT ANY WARRANTY; without even the implied warranty of
/// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
///
/// See the GNU General Public License for more details.
///
/// You should have received a copy of the GNU General Public License
/// along with this program; if not, write to the Free Software
/// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
/// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using SharpQuake.Renderer.OpenGL.Models;

namespace SharpQuake.Renderer.OpenGL
{
    public class Shader : IShader
    {
        public readonly int Handle;

        private readonly Dictionary<string, int> _uniformLocations;

        public Shader( string vertexShaderSource, string fragShaderSource )
        {
            var vertexShader = CreateShader( ShaderType.VertexShader, vertexShaderSource );
            var fragmentShader = CreateShader( ShaderType.FragmentShader, fragShaderSource );

            Handle = GL.CreateProgram( );

            GL.AttachShader( Handle, vertexShader );
            GL.AttachShader( Handle, fragmentShader );

            try
            {
                LinkProgram( Handle );
            }
            finally
            {
                GL.DetachShader( Handle, vertexShader );
                GL.DetachShader( Handle, fragmentShader );
                GL.DeleteShader( vertexShader );
                GL.DeleteShader( fragmentShader );
            }

            _uniformLocations = GetUniformLocations( Handle );
        }

        public void Use( )
        {
            GL.UseProgram( Handle );
        }

        public int GetAttribLocation( string attribName )
        {
            return GL.GetAttribLocation( Handle, attribName );
        }

        public int GetUniformLocation( string name )
        {
            return GL.GetUniformLocation( Handle, name );
        }

        public void SetInt32( string name, int data )
        {
            Use( );
            GL.Uniform1( GetCachedUniformLocation( name ), data );
        }

        public void SetSingle( string name, float data )
        {
            Use( );
            GL.Uniform1( GetCachedUniformLocation( name ), data );
        }

        public void SetMatrix4( string name, Matrix4 data )
        {
            Use( );
            GL.UniformMatrix4( GetCachedUniformLocation( name ), true, ref data );
        }

        public void SetVector3( string name, Vector3 data )
        {
            Use( );
            GL.Uniform3( GetCachedUniformLocation( name ), data );
        }

        public void SetVector2( string name, Vector2 data )
        {
            Use( );
            GL.Uniform2( GetCachedUniformLocation( name ), data );
        }

        public static Shader FromResource( string shaderName )
        {
            return new Shader(
                LoadShaderResource( $"{shaderName}.vert" ),
                LoadShaderResource( $"{shaderName}.frag" ) );
        }

        private int GetCachedUniformLocation( string name )
        {
            return _uniformLocations[name];
        }

        private static int CreateShader( ShaderType type, string source )
        {
            var shader = GL.CreateShader( type );

            GL.ShaderSource( shader, source );
            CompileShader( shader );

            return shader;
        }

        private static void CompileShader( int shader )
        {
            GL.CompileShader( shader );
            GL.GetShader( shader, ShaderParameter.CompileStatus, out var code );

            if ( code == ( int ) All.True )
            {
                return;
            }

            var infoLog = GL.GetShaderInfoLog( shader );

            if ( string.IsNullOrEmpty( infoLog ) )
            {
                throw new Exception( $"Error occurred whilst compiling Shader({shader})" );
            }

            throw new Exception( $"Error occurred whilst compiling Shader({shader}): {infoLog}" );
        }

        private static void LinkProgram( int program )
        {
            GL.LinkProgram( program );
            GL.GetProgram( program, GetProgramParameterName.LinkStatus, out var code );

            if ( code == ( int ) All.True )
            {
                return;
            }

            var infoLog = GL.GetProgramInfoLog( program );

            if ( string.IsNullOrEmpty( infoLog ) )
            {
                throw new Exception( $"Error occurred whilst linking Program({program})" );
            }

            throw new Exception( $"Error occurred whilst linking Program({program}): {infoLog}" );
        }

        private static Dictionary<string, int> GetUniformLocations( int program )
        {
            GL.GetProgram( program, GetProgramParameterName.ActiveUniforms, out var uniformCount );

            var locations = new Dictionary<string, int>( );

            for ( var i = 0; i < uniformCount; i++ )
            {
                var name = GL.GetActiveUniform( program, i, out _, out _ );
                var location = GL.GetUniformLocation( program, name );

                locations.Add( name, location );
            }

            return locations;
        }

        private static string LoadShaderResource( string shaderFile )
        {
            var assembly = typeof( GLModelBuffer ).Assembly;
            var resourcePath = $"SharpQuake.Renderer.OpenGL.Resources.GLSL.{shaderFile}";

            using ( var stream = assembly.GetManifestResourceStream( resourcePath ) )
            {
                if ( stream == null )
                {
                    return null;
                }

                using ( var reader = new StreamReader( stream ) )
                {
                    return reader.ReadToEnd( );
                }
            }
        }
    }
}