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

using OpenTK.Graphics.OpenGL;
using SharpQuake.Renderer.OpenGL;
using SharpQuake.Renderer.OpenGL.Textures;
using SharpQuake.Renderer.Textures;
using System;

namespace SharpQuake.Renderer.OpenGL
{
    internal sealed class GLPostProcessor : IDisposable
    {
        private readonly Action<IRenderTexture> _drawTexture2D;

        private Int32 _width;
        private Int32 _height;
        private System.Boolean _isInitialised;

        private Int32 _sceneFrameBuffer;
        private IRenderTexture _sceneTexture;
        private Int32 _sceneDepthBuffer;

        private Int32 _blurFrameBufferA;
        private Int32 _blurFrameBufferB;
        private IRenderTexture _blurTextureA;
        private IRenderTexture _blurTextureB;

        private Shader _postProcessShaderProgram;
        private Shader _blurShaderProgram;

        public GLPostProcessor( Action<IRenderTexture> drawTexture2D )
        {
            _drawTexture2D = drawTexture2D ?? throw new ArgumentNullException( nameof( drawTexture2D ) );
        }

        public IRenderTexture SceneTexture
        {
            get
            {
                return _sceneTexture;
            }
        }

        public void Initialise( Int32 width, Int32 height )
        {
            if ( width <= 0 || height <= 0 )
                return;

            if ( _isInitialised && _width == width && _height == height )
                return;

            DisposeBuffers( );

            _width = width;
            _height = height;

            _sceneTexture = CreateRenderTexture( width, height );
            _blurTextureA = CreateRenderTexture( width, height );
            _blurTextureB = CreateRenderTexture( width, height );

            _sceneFrameBuffer = CreateSceneFrameBuffer( _sceneTexture );
            _blurFrameBufferA = CreateColorFrameBuffer( _blurTextureA );
            _blurFrameBufferB = CreateColorFrameBuffer( _blurTextureB );

            if ( _postProcessShaderProgram == null )
                _postProcessShaderProgram = Shader.FromResource( "PostFX" );

            if ( _blurShaderProgram == null )
                _blurShaderProgram = Shader.FromResource( "Blur" );

            _isInitialised = true;
        }

        public void Invalidate( )
        {
            DisposeBuffers( );
        }

        public void BeginCapture( )
        {
            if ( !_isInitialised || _sceneFrameBuffer == 0 )
                return;

            GL.BindFramebuffer( FramebufferTarget.Framebuffer, _sceneFrameBuffer );
            GL.DrawBuffer( DrawBufferMode.ColorAttachment0 );
        }

        public void EndCapture( )
        {
            GL.BindFramebuffer( FramebufferTarget.Framebuffer, 0 );
            GL.DrawBuffer( DrawBufferMode.Back );
        }

        public void Render(
            Double time,
            Single noiseGrain,
            Single screenBlurAmount,
            Single bloomIntensity,
            Single blurRadius,
            Int32 blurIterations,
            bool fadeScreen )
        {
            if ( !_isInitialised || _sceneTexture == null )
                return;

            screenBlurAmount = Math.Clamp( screenBlurAmount, 0.0f, 1.0f );
            bloomIntensity = Math.Max( 0.0f, bloomIntensity );
            noiseGrain = Math.Max( 0.0f, noiseGrain );
            blurRadius = Math.Max( 0.0f, blurRadius );
            blurIterations = Math.Max( 1, blurIterations );

            var needsBlur =
                screenBlurAmount > 0.0f ||
                bloomIntensity > 0.0f;

            IRenderTexture blurredTexture = _sceneTexture;

            if ( needsBlur )
                blurredTexture = GenerateBlurTexture( _sceneTexture, blurRadius, blurIterations );

            GL.BindFramebuffer( FramebufferTarget.Framebuffer, 0 );
            GL.DrawBuffer( DrawBufferMode.Back );

            SetupPostProcessState( _width, _height );

            _postProcessShaderProgram.Use( );

            _postProcessShaderProgram.SetInt32( "tex", 0 );
            _postProcessShaderProgram.SetInt32( "blurTex", 1 );

            _postProcessShaderProgram.SetSingle( "noiseGrain", noiseGrain );
            _postProcessShaderProgram.SetSingle( "time", ( Single ) time );

            _postProcessShaderProgram.SetSingle( "screenBlurAmount", screenBlurAmount );
            _postProcessShaderProgram.SetSingle( "bloomIntensity", bloomIntensity );
            _postProcessShaderProgram.SetInt32( "blurEnabled", needsBlur ? 1 : 0 );
            _postProcessShaderProgram.SetSingle( "fadeScreen", fadeScreen ? 0.5f : 0 );

            GL.ActiveTexture( TextureUnit.Texture1 );
            GL.BindTexture( TextureTarget.Texture2D, blurredTexture.ID );

            GL.ActiveTexture( TextureUnit.Texture0 );
            GL.ClientActiveTexture( TextureUnit.Texture0 );

            _drawTexture2D( _sceneTexture );

            GL.ActiveTexture( TextureUnit.Texture1 );
            GL.BindTexture( TextureTarget.Texture2D, 0 );

            GL.ActiveTexture( TextureUnit.Texture0 );

            GL.UseProgram( 0 );
        }

        private IRenderTexture GenerateBlurTexture(
            IRenderTexture source,
            Single blurRadius,
            Int32 iterations )
        {
            IRenderTexture currentSource = source;

            for ( var i = 0; i < iterations; i++ )
            {
                RenderBlurPass(
                    currentSource,
                    _blurFrameBufferA,
                    _blurTextureA,
                    1.0f,
                    0.0f,
                    blurRadius );

                RenderBlurPass(
                    _blurTextureA,
                    _blurFrameBufferB,
                    _blurTextureB,
                    0.0f,
                    1.0f,
                    blurRadius );

                currentSource = _blurTextureB;
            }

            return _blurTextureB;
        }

        private void RenderBlurPass(
            IRenderTexture source,
            Int32 targetFrameBuffer,
            IRenderTexture targetTexture,
            Single directionX,
            Single directionY,
            Single blurRadius )
        {
            GL.BindFramebuffer( FramebufferTarget.Framebuffer, targetFrameBuffer );
            GL.DrawBuffer( DrawBufferMode.ColorAttachment0 );

            SetupPostProcessState( targetTexture.Width, targetTexture.Height );

            GL.ActiveTexture( TextureUnit.Texture0 );
            GL.ClientActiveTexture( TextureUnit.Texture0 );

            _blurShaderProgram.Use( );
            _blurShaderProgram.SetInt32( "tex", 0 );
            _blurShaderProgram.SetSingle( "texelWidth", 1.0f / source.Width );
            _blurShaderProgram.SetSingle( "texelHeight", 1.0f / source.Height );
            _blurShaderProgram.SetSingle( "directionX", directionX );
            _blurShaderProgram.SetSingle( "directionY", directionY );
            _blurShaderProgram.SetSingle( "blurRadius", blurRadius );

            _drawTexture2D( source );

            GL.UseProgram( 0 );
        }

        private static void SetupPostProcessState( Int32 width, Int32 height )
        {
            GL.Viewport( 0, 0, width, height );

            GL.MatrixMode( MatrixMode.Projection );
            GL.LoadIdentity( );
            GL.Ortho( 0, width, height, 0, -1, 1 );

            GL.MatrixMode( MatrixMode.Modelview );
            GL.LoadIdentity( );

            GL.Disable( EnableCap.DepthTest );
            GL.Disable( EnableCap.CullFace );
            GL.Disable( EnableCap.Blend );
            GL.Disable( EnableCap.AlphaTest );

            GL.Color4( 1.0f, 1.0f, 1.0f, 1.0f );
        }

        private IRenderTexture CreateRenderTexture( Int32 width, Int32 height )
        {
            var texture = new GLRenderTexture( GL.GenTexture( ), width, height );

            GL.BindTexture( TextureTarget.Texture2D, texture.ID );

            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.Rgb,
                width,
                height,
                0,
                PixelFormat.Rgb,
                PixelType.UnsignedByte,
                IntPtr.Zero );

            GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, ( Int32 ) TextureMinFilter.Linear );
            GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, ( Int32 ) TextureMagFilter.Linear );
            GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureWrapS, ( Int32 ) TextureWrapMode.ClampToEdge );
            GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureWrapT, ( Int32 ) TextureWrapMode.ClampToEdge );

            GL.BindTexture( TextureTarget.Texture2D, 0 );

            return texture;
        }

        private Int32 CreateSceneFrameBuffer( IRenderTexture texture )
        {
            var frameBuffer = GL.GenFramebuffer( );

            GL.BindFramebuffer( FramebufferTarget.Framebuffer, frameBuffer );
            GL.DrawBuffer( DrawBufferMode.ColorAttachment0 );

            GL.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D,
                texture.ID,
                0 );

            _sceneDepthBuffer = GL.GenRenderbuffer( );
            GL.BindRenderbuffer( RenderbufferTarget.Renderbuffer, _sceneDepthBuffer );

            GL.RenderbufferStorage(
                RenderbufferTarget.Renderbuffer,
                RenderbufferStorage.DepthComponent24,
                texture.Width,
                texture.Height );

            GL.FramebufferRenderbuffer(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.DepthAttachment,
                RenderbufferTarget.Renderbuffer,
                _sceneDepthBuffer );

            CheckFrameBuffer( "Scene framebuffer" );

            GL.BindFramebuffer( FramebufferTarget.Framebuffer, 0 );

            return frameBuffer;
        }

        private Int32 CreateColorFrameBuffer( IRenderTexture texture )
        {
            var frameBuffer = GL.GenFramebuffer( );

            GL.BindFramebuffer( FramebufferTarget.Framebuffer, frameBuffer );
            GL.DrawBuffer( DrawBufferMode.ColorAttachment0 );

            GL.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D,
                texture.ID,
                0 );

            CheckFrameBuffer( "Colour framebuffer" );

            GL.BindFramebuffer( FramebufferTarget.Framebuffer, 0 );

            return frameBuffer;
        }

        private static void CheckFrameBuffer( String name )
        {
            var status = GL.CheckFramebufferStatus( FramebufferTarget.Framebuffer );

            if ( status != FramebufferErrorCode.FramebufferComplete )
            {
                Console.WriteLine( "{0} status: {1}", name, status );
                throw new Exception( name + " not complete: " + status );
            }
        }

        private void DisposeBuffers( )
        {
            if ( _sceneFrameBuffer != 0 )
            {
                GL.DeleteFramebuffer( _sceneFrameBuffer );
                _sceneFrameBuffer = 0;
            }

            if ( _blurFrameBufferA != 0 )
            {
                GL.DeleteFramebuffer( _blurFrameBufferA );
                _blurFrameBufferA = 0;
            }

            if ( _blurFrameBufferB != 0 )
            {
                GL.DeleteFramebuffer( _blurFrameBufferB );
                _blurFrameBufferB = 0;
            }

            if ( _sceneTexture != null && _sceneTexture.ID != 0 )
            {
                GL.DeleteTexture( _sceneTexture.ID );
                _sceneTexture = null;
            }

            if ( _blurTextureA != null && _blurTextureA.ID != 0 )
            {
                GL.DeleteTexture( _blurTextureA.ID );
                _blurTextureA = null;
            }

            if ( _blurTextureB != null && _blurTextureB.ID != 0 )
            {
                GL.DeleteTexture( _blurTextureB.ID );
                _blurTextureB = null;
            }

            if ( _sceneDepthBuffer != 0 )
            {
                GL.DeleteRenderbuffer( _sceneDepthBuffer );
                _sceneDepthBuffer = 0;
            }

            _width = 0;
            _height = 0;
            _isInitialised = false;
        }

        public void Dispose( )
        {
            DisposeBuffers( );

            // If your Shader class exposes a Dispose/Delete method,
            // call it here for _postProcessShaderProgram and _blurShaderProgram.
            _postProcessShaderProgram = null;
            _blurShaderProgram = null;
        }
    }
}