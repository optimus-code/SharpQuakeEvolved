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
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using SharpQuake.Framework;
using SharpQuake.Renderer.Models;
using SharpQuake.Renderer.Textures;

namespace SharpQuake.Renderer.OpenGL.Models
{
    public class GLModelBuffer : BaseModelBuffer
    {
        private Int32 VertexArrayID
        {
            get;
            set;
        }

        private Int32 VertexBufferID
        {
            get;
            set;
        }

        private Int32 IndexBufferID
        {
            get;
            set;
        }

        private Shader Shader
        {
            get;
            set;
        }

        private readonly GLDevice _glDevice;

        public GLModelBuffer( BaseDevice device, BufferVertex[] vertices, UInt32[] indices, string shaderName = "DefaultSurface" ) 
            : base( device, vertices, indices, shaderName )
        {
            _glDevice = ( GLDevice ) device;
            Initialise( );
        }

        private void Initialise()
        {            
            Shader = Shader.FromResource( ShaderName );

            BuildBuffers( );
        }

        private void BuildBuffers( )
        {
            var vertexSize = Marshal.SizeOf<BufferVertex>( );

            VertexBufferID = GL.GenBuffer( );
            GL.BindBuffer( BufferTarget.ArrayBuffer, VertexBufferID );
            GL.BufferData( BufferTarget.ArrayBuffer, Vertices.Length * vertexSize, Vertices, BufferUsageHint.StaticDraw );

            IndexBufferID = GL.GenBuffer( );
            GL.BindBuffer( BufferTarget.ElementArrayBuffer, IndexBufferID );
            GL.BufferData( BufferTarget.ElementArrayBuffer, Indices.Length * sizeof( uint ), Indices, BufferUsageHint.StaticDraw );

            GL.BindBuffer( BufferTarget.ArrayBuffer, 0 );
            GL.BindBuffer( BufferTarget.ElementArrayBuffer, 0 );
        }


        public override void Begin( )
        {
            var vertexSize = Marshal.SizeOf<BufferVertex>( );

            GL.Enable( EnableCap.Texture2D );

            GL.EnableClientState( ArrayCap.VertexArray );
            GL.EnableClientState( ArrayCap.TextureCoordArray );

            GL.BindBuffer( BufferTarget.ArrayBuffer, VertexBufferID );

            GL.VertexPointer( 3, VertexPointerType.Float, vertexSize, IntPtr.Zero );

            GL.ClientActiveTexture( TextureUnit.Texture0 );
            GL.EnableClientState( ArrayCap.TextureCoordArray );
            GL.TexCoordPointer( 2, TexCoordPointerType.Float, vertexSize, ( IntPtr ) ( Marshal.SizeOf<float>( ) * 3 ) );

            GL.ClientActiveTexture( TextureUnit.Texture1 );
            GL.EnableClientState( ArrayCap.TextureCoordArray );
            GL.TexCoordPointer( 2, TexCoordPointerType.Float, vertexSize, ( IntPtr ) ( Marshal.SizeOf<float>( ) * 5 ) );

            GL.BindBuffer( BufferTarget.ElementArrayBuffer, IndexBufferID );

            Shader.Use( );
        }


        public override void End( )
        {
            GL.Disable( EnableCap.Texture2D );

            GL.DisableClientState( ArrayCap.VertexArray );
            GL.DisableClientState( ArrayCap.TextureCoordArray );

            GL.BindBuffer( BufferTarget.ArrayBuffer, 0 );

            GL.BindTexture( TextureTarget.Texture2D, 0 );
            GL.ActiveTexture( TextureUnit.Texture0 );
            GL.UseProgram( 0 );
        }

        private BaseTexture ActiveTex;

        public override void BeginTexture( BaseTexture texture, BaseTexture lightmapTexture, Double time, bool noLightmap, bool waveDistort, Double waveScale )
        {
            ActiveTex = texture;

            if ( ActiveTex == null )
                return;

            GL.ActiveTexture( TextureUnit.Texture0 );
            ActiveTex?.Bind( );
            Shader.SetInt32( "tex", 0 );

            if ( noLightmap )
            {
                Shader.SetInt32( "noLm", 1 );
                Shader.SetInt32( "lm", 0 );
            }
            else
            {
                Shader.SetInt32( "noLm", 0 );

                GL.ActiveTexture( TextureUnit.Texture1 );
                lightmapTexture.Bind( );

                Shader.SetInt32( "lm", 1 );
            }

            if ( waveDistort )
            {
                Shader.SetInt32( "waveDistort", 1 );
                Shader.SetSingle( "time", ( Single ) time );
                Shader.SetSingle( "turbScale", ( Single ) waveScale );
            }
            else
            {
                Shader.SetInt32( "waveDistort", 0 );
                Shader.SetSingle( "time", 0 );
                Shader.SetSingle( "turbScale", 0 );
            }
        }

        public override void DrawPoly( GLPoly poly )
        {
            var indexOffset = poly.FirstIndex * sizeof( uint ); // For UnsignedInt (4 bytes)

            GL.DrawElements(
               PrimitiveType.TriangleFan,
               poly.NumIndices,
               DrawElementsType.UnsignedInt,
               ( IntPtr ) indexOffset
           );
        }

        public override void Draw( )
        {
        }

        public override void Dispose( )
        {
            base.Dispose( );

            GL.BindBuffer( BufferTarget.ArrayBuffer, 0 );
            GL.DeleteBuffer( VertexBufferID );

            GL.BindBuffer( BufferTarget.ElementArrayBuffer, 0 );
            GL.DeleteBuffer( IndexBufferID );
        }
    }
}
