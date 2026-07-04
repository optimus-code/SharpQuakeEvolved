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
/// </copyright>

using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Desktop;
using SharpQuake.Framework;
using SharpQuake.Framework.IO;
using SharpQuake.Framework.Mathematics;
using SharpQuake.Renderer.OpenGL.Models;
using SharpQuake.Renderer.OpenGL.Textures;
using SharpQuake.Renderer.Textures;
using StbImageWriteSharp;
using System;
using System.Collections.Generic;
using System.IO;
using Boolean = System.Boolean;
using Buffer = System.Buffer;
using TKGameWindow = OpenTK.Windowing.Desktop.GameWindow;
using TKMathHelper = OpenTK.Mathematics.MathHelper;
using TKMatrix4 = OpenTK.Mathematics.Matrix4;
using TKVector2i = OpenTK.Mathematics.Vector2i;
using TKVector3 = OpenTK.Mathematics.Vector3;
using TKWindowBorder = OpenTK.Windowing.Common.WindowBorder;
using TKWindowState = OpenTK.Windowing.Common.WindowState;

namespace SharpQuake.Renderer.OpenGL
{
    public class GLDevice : BaseDevice
    {
        private TKGameWindow Form
        {
            get;
            set;
        }

        private MonitorInfo Monitor
        {
            get;
            set;
        }

        public TKMatrix4 Projection;
        public TKMatrix4 View;
        public TKMatrix4 WorldMatrix; // r_world_matrix

        private int _frameBuffer;
        private IRenderTexture _renderTexture;
        private int _depthBuffer;
        private Shader _postProcessShaderProgram;
        private bool _hasInitialisedRenderBuffers;

        public GLDevice( TKGameWindow form )
            : this( form, Monitors.GetPrimaryMonitor( ) )
        {
        }

        public GLDevice( TKGameWindow form, MonitorInfo monitor )
            : base( typeof( GLDeviceDesc ),
                  typeof( GLGraphics ),
                  typeof( GLTextureAtlas ),
                  typeof( GLModel ),
                  typeof( GLModelDesc ),
                  typeof( GLAliasModel ),
                  typeof( GLAliasModelDesc ),
                  typeof( GLTexture ),
                  typeof( GLTextureDesc ),
                  typeof( GLModelBuffer ) )
        {
            Form = form;
            Monitor = monitor ?? Monitors.GetPrimaryMonitor( );

            TextureFilters = new Dictionary<String, BaseTextureFilter>
            {
                { "GL_NEAREST", new GLTextureFilter( "GL_NEAREST", TextureMinFilter.Nearest, TextureMagFilter.Nearest ) },
                { "GL_LINEAR", new GLTextureFilter( "GL_LINEAR", TextureMinFilter.Linear, TextureMagFilter.Linear ) },
                { "GL_NEAREST_MIPMAP_NEAREST", new GLTextureFilter( "GL_NEAREST_MIPMAP_NEAREST", TextureMinFilter.NearestMipmapNearest, TextureMagFilter.Nearest ) },
                { "GL_LINEAR_MIPMAP_NEAREST", new GLTextureFilter( "GL_LINEAR_MIPMAP_NEAREST", TextureMinFilter.LinearMipmapNearest, TextureMagFilter.Linear ) },
                { "GL_NEAREST_MIPMAP_LINEAR", new GLTextureFilter( "GL_NEAREST_MIPMAP_LINEAR", TextureMinFilter.NearestMipmapLinear, TextureMagFilter.Nearest ) },
                { "GL_LINEAR_MIPMAP_LINEAR", new GLTextureFilter( "GL_LINEAR_MIPMAP_LINEAR", TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Linear ) }
            };

            BlendModes = new Dictionary<String, BaseTextureBlendMode>
            {
                { "GL_MODULATE", new GLTextureBlendMode( "GL_MODULATE", TextureEnvMode.Modulate ) },
                { "GL_ADD", new GLTextureBlendMode( "GL_ADD", TextureEnvMode.Add ) },
                { "GL_REPLACE", new GLTextureBlendMode( "GL_REPLACE", TextureEnvMode.Replace ) },
                { "GL_DECAL", new GLTextureBlendMode( "GL_DECAL",  TextureEnvMode.Decal ) },
                { "GL_REPLACE_EXT", new GLTextureBlendMode( "GL_REPLACE_EXT", TextureEnvMode.ReplaceExt ) },
                { "GL_TEXTURE_ENV_BIAS_SGIX", new GLTextureBlendMode( "GL_TEXTURE_ENV_BIAS_SGIX", TextureEnvMode.TextureEnvBiasSgix ) },
                { "GL_COMBINE", new GLTextureBlendMode( "GL_COMBINE", TextureEnvMode.Combine ) }
            };

            PixelFormats = new GLPixelFormat[]
            {
                new GLPixelFormat( "GL_LUMINANCE", PixelFormat.Luminance ),
                new GLPixelFormat( "GL_RGBA", PixelFormat.Rgba ),
                new GLPixelFormat( "GL_RGB", PixelFormat.Rgb ),
                new GLPixelFormat( "GL_BGR", PixelFormat.Bgr ),
                new GLPixelFormat( "GL_BGRA", PixelFormat.Bgra ),
                new GLPixelFormat( "GL_ALPHA", PixelFormat.Alpha )
            };
        }

        /// <summary>
        /// GL_Init
        /// </summary>
        public override void Initialise( Byte[] palette )
        {
            base.Initialise( palette );

            GL.ClearColor( 1, 0, 0, 0 );
            GL.CullFace( CullFaceMode.Front );
            GL.Enable( EnableCap.Texture2D );

            GL.Enable( EnableCap.AlphaTest );
            GL.AlphaFunc( AlphaFunction.Greater, 0.666f );

            GL.PolygonMode( MaterialFace.FrontAndBack, PolygonMode.Fill );
            GL.ShadeModel( ShadingModel.Flat );

            GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, ( Int32 ) TextureMinFilter.Nearest );
            GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, ( Int32 ) TextureMagFilter.Nearest );

            GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureWrapS, ( Int32 ) TextureWrapMode.Repeat );
            GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureWrapT, ( Int32 ) TextureWrapMode.Repeat );
            GL.BlendFunc( BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha );
            GL.TexEnv( TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, ( Int32 ) TextureEnvMode.Replace );

            _hasInitialisedRenderBuffers = false;
        }

        private void ConfigureFrameBuffers( )
        {
            if ( _hasInitialisedRenderBuffers )
                return;

            DisposeFrameBuffers( );

            _frameBuffer = GL.GenFramebuffer( );
            GL.BindFramebuffer( FramebufferTarget.Framebuffer, _frameBuffer );

            GLTexture.CurrentTextureNumber++;
            _renderTexture = new GLRenderTexture( GL.GenTexture( ), Desc.ActualWidth, Desc.ActualHeight );

            GL.BindTexture( TextureTarget.Texture2D, _renderTexture.ID );

            GL.TexImage2D( TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb, _renderTexture.Width, _renderTexture.Height, 0, PixelFormat.Rgb, PixelType.UnsignedByte, IntPtr.Zero );
            GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, ( Int32 ) TextureMinFilter.Linear );
            GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, ( Int32 ) TextureMagFilter.Linear );
            GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureWrapS, ( Int32 ) TextureWrapMode.ClampToEdge );
            GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureWrapT, ( Int32 ) TextureWrapMode.ClampToEdge );

            GL.FramebufferTexture2D( FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _renderTexture.ID, 0 );

            _depthBuffer = GL.GenRenderbuffer( );
            GL.BindRenderbuffer( RenderbufferTarget.Renderbuffer, _depthBuffer );
            GL.RenderbufferStorage( RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, _renderTexture.Width, _renderTexture.Height );
            GL.FramebufferRenderbuffer( FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _depthBuffer );

            FramebufferErrorCode status = GL.CheckFramebufferStatus( FramebufferTarget.Framebuffer );
            if ( status != FramebufferErrorCode.FramebufferComplete )
            {
                Console.WriteLine( "Framebuffer status: " + status );
                throw new Exception( "Framebuffer not complete: " + status );
            }

            GL.BindFramebuffer( FramebufferTarget.Framebuffer, 0 );

            _postProcessShaderProgram = Shader.FromResource( "PostFX" );
            _hasInitialisedRenderBuffers = true;
        }

        private void DisposeFrameBuffers( )
        {
            if ( _frameBuffer != 0 )
            {
                GL.DeleteFramebuffer( _frameBuffer );
                _frameBuffer = 0;
            }

            if ( _renderTexture != null && _renderTexture.ID != 0 )
            {
                GL.DeleteTexture( _renderTexture.ID );
                _renderTexture = null;
            }

            if ( _depthBuffer != 0 )
            {
                GL.DeleteRenderbuffer( _depthBuffer );
                _depthBuffer = 0;
            }
        }

        private void CheckGLError( String operation )
        {
            ErrorCode error;
            while ( ( error = GL.GetError( ) ) != ErrorCode.NoError )
            {
                Console.WriteLine( $"{operation}: OpenGL Error {error}" );
            }
        }

        public void SetTextureFilters( TextureMinFilter min, TextureMagFilter mag )
        {
            GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, ( Int32 ) min );
            GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, ( Int32 ) mag );
        }

        public override void SetTextureFilters( String name )
        {
            var filter = ( GLTextureFilter ) GetTextureFilters( name );

            if ( filter != null )
                SetTextureFilters( filter.Minimise, filter.Maximise );
        }

        public void SetBlendMode( TextureEnvMode mode )
        {
            GL.TexEnv( TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, ( Int32 ) mode );
        }

        public override void SetBlendMode( String name )
        {
            var mode = ( GLTextureBlendMode ) GetBlendMode( name );

            if ( mode != null )
                SetBlendMode( mode.Mode );
        }

        protected override void GetAvailableModes( )
        {
            var monitor = Monitor ?? Monitors.GetPrimaryMonitor( );
            var tmp = new List<VideoMode>( monitor.SupportedVideoModes.Count );

            foreach ( var res in monitor.SupportedVideoModes )
            {
                var bitsPerPixel = res.RedBits + res.GreenBits + res.BlueBits;

                if ( bitsPerPixel <= 8 )
                    continue;

                Predicate<VideoMode> sameMode = delegate ( VideoMode m )
                {
                    return ( m.Width == res.Width && m.Height == res.Height && m.BitsPerPixel == bitsPerPixel );
                };

                if ( tmp.Exists( sameMode ) )
                    continue;

                var mode = new VideoMode( );
                mode.Width = res.Width;
                mode.Height = res.Height;
                mode.BitsPerPixel = bitsPerPixel;
                mode.RefreshRate = res.RefreshRate;
                tmp.Add( mode );
            }

            AvailableModes = tmp.ToArray( );

            var current = monitor.CurrentVideoMode;

            FirstAvailableMode = new()
            {
                Width = current.Width,
                Height = current.Height,
                BitsPerPixel = current.RedBits + current.GreenBits + current.BlueBits,
                RefreshRate = current.RefreshRate,
                FullScreen = true
            };
        }

        public override void SetMode( Int32 index, Byte[] palette )
        {
            base.SetMode( index, palette );
            _hasInitialisedRenderBuffers = false;
        }

        protected override void ChangeMode( VideoMode mode )
        {
            try
            {
                if ( Desc.IsFullScreen )
                {
                    var monitor = Monitor ?? Monitors.GetPrimaryMonitor( );
                    Form.WindowBorder = TKWindowBorder.Hidden;
                    Form.MakeFullscreen( monitor.Handle, mode.Width, mode.Height, mode.RefreshRate );
                }
                else
                {
                    Form.WindowState = TKWindowState.Normal;
                    Form.WindowBorder = TKWindowBorder.Fixed;
                    Form.ClientSize = new TKVector2i( mode.Width, mode.Height );
                }
            }
            catch ( Exception ex )
            {
                Utilities.Error( $"Couldn't set video mode: {ex.Message}" );
            }

            Desc.ActualWidth = Form.ClientSize.X;
            Desc.ActualHeight = Form.ClientSize.Y;
            _hasInitialisedRenderBuffers = false;
        }

        public override void BeginScene( )
        {
            base.BeginScene( );

            GL.Color3( 1f, 1, 1 );
        }

        public override void EndScene( )
        {
            base.EndScene( );
        }

        public override void ResetMatrix( )
        {
            GL.LoadMatrix( ref WorldMatrix );
        }

        public override void PushMatrix( )
        {
            GL.PushMatrix( );
        }

        public override void PopMatrix( )
        {
            GL.PopMatrix( );
        }

        protected override void Present( )
        {
            Form?.SwapBuffers( );
        }

        public override void SetZWrite( Boolean enable )
        {
            GL.DepthMask( enable );
        }

        public override void SetViewport( Int32 x, Int32 y, Int32 width, Int32 height )
        {
            GL.Viewport( x, y, width, height );
        }

        public override void Begin2DScene( )
        {
            End3DRenderTarget( );

            SetViewport( Desc.ViewRect );

            GL.MatrixMode( MatrixMode.Projection );
            GL.LoadIdentity( );
            GL.Ortho( 0, Desc.Width, Desc.Height, 0, -99999, 99999 );

            GL.MatrixMode( MatrixMode.Modelview );
            GL.LoadIdentity( );

            GL.Disable( EnableCap.DepthTest );
            GL.Disable( EnableCap.CullFace );
            GL.Disable( EnableCap.Blend );
            GL.Enable( EnableCap.AlphaTest );

            GL.Color4( 1.0f, 1.0f, 1.0f, 1.0f );

            RenderPostFX( );
        }

        public override void End2DScene( )
        {
        }

        public override void Begin3DRenderTarget( )
        {
            GL.BindFramebuffer( FramebufferTarget.Framebuffer, _frameBuffer );
        }

        public override void End3DRenderTarget( )
        {
            GL.BindFramebuffer( FramebufferTarget.Framebuffer, 0 );
        }

        public override void RenderPostFX( )
        {
            if ( _renderTexture == null )
                return;

            _postProcessShaderProgram.Use( );

            Graphics.DrawTexture2D( _renderTexture );

            GL.UseProgram( 0 );
        }

        public override void Setup3DScene( Boolean cull, refdef_t renderDef, Boolean isEnvMap )
        {
            GL.MatrixMode( MatrixMode.Projection );
            GL.LoadIdentity( );

            var x = renderDef.vrect.x * Desc.ActualWidth / Desc.Width;
            var x2 = ( renderDef.vrect.x + renderDef.vrect.width ) * Desc.ActualWidth / Desc.Width;
            var y = ( Desc.Height - renderDef.vrect.y ) * Desc.ActualHeight / Desc.Height;
            var y2 = ( Desc.Height - ( renderDef.vrect.y + renderDef.vrect.height ) ) * Desc.ActualHeight / Desc.Height;

            if ( x > 0 )
                x--;
            if ( x2 < Desc.ActualWidth )
                x2++;
            if ( y2 < 0 )
                y2--;
            if ( y < Desc.ActualHeight )
                y++;

            var w = x2 - x;
            var h = y - y2;

            if ( isEnvMap )
            {
                x = y2 = 0;
                w = h = 256;
            }

            GL.Viewport( x, y2, w, h );

            var screenaspect = ( Single ) renderDef.vrect.width / renderDef.vrect.height;

            BuildProjectionMatrix( renderDef.fov_y, screenaspect, 4, 4096 );

            GL.CullFace( CullFaceMode.Front );

            GL.MatrixMode( MatrixMode.Modelview );
            GL.LoadIdentity( );

            BuildViewMatrix( renderDef );

            WorldMatrix = View * Projection;

            GL.MatrixMode( MatrixMode.Projection );
            GL.LoadMatrix( ref Projection );

            GL.MatrixMode( MatrixMode.Modelview );
            GL.LoadMatrix( ref View );

            if ( cull )
                GL.Enable( EnableCap.CullFace );
            else
                GL.Disable( EnableCap.CullFace );

            GL.Disable( EnableCap.Blend );
            GL.Disable( EnableCap.AlphaTest );
            GL.Enable( EnableCap.DepthTest );

            ConfigureFrameBuffers( );
            Begin3DRenderTarget( );
        }

        /// <summary>
        /// Build view matrix.
        /// </summary>
        private void BuildViewMatrix( refdef_t renderDef )
        {
            View =
                TKMatrix4.CreateTranslation( new TKVector3( -renderDef.vieworg.X, -renderDef.vieworg.Y, -renderDef.vieworg.Z ) ) *
                TKMatrix4.CreateRotationZ( TKMathHelper.DegreesToRadians( -renderDef.viewangles.Y ) ) *
                TKMatrix4.CreateRotationY( TKMathHelper.DegreesToRadians( -renderDef.viewangles.X ) ) *
                TKMatrix4.CreateRotationX( TKMathHelper.DegreesToRadians( -renderDef.viewangles.Z ) ) *
                TKMatrix4.CreateRotationZ( TKMathHelper.DegreesToRadians( 90f ) ) *
                TKMatrix4.CreateRotationX( TKMathHelper.DegreesToRadians( -90f ) );
        }

        private void BuildProjectionMatrix( Single fovy, Single aspect, Single zNear, Single zFar )
        {
            Projection = TKMatrix4.CreatePerspectiveFieldOfView( TKMathHelper.DegreesToRadians( fovy ), aspect, zNear, zFar );
        }

        public override void Clear( Boolean zTrick, Single clear )
        {
            if ( zTrick )
            {
                if ( clear != 0 )
                    GL.Clear( ClearBufferMask.ColorBufferBit );

                Desc.TrickFrame++;
                if ( ( Desc.TrickFrame & 1 ) != 0 )
                {
                    Desc.DepthMinimum = 0;
                    Desc.DepthMaximum = 0.49999f;
                    GL.DepthFunc( DepthFunction.Lequal );
                }
                else
                {
                    Desc.DepthMinimum = 1;
                    Desc.DepthMaximum = 0.5f;
                    GL.DepthFunc( DepthFunction.Gequal );
                }
            }
            else
            {
                if ( clear != 0 )
                    GL.Clear( ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit );
                else
                    GL.Clear( ClearBufferMask.DepthBufferBit );

                Desc.DepthMinimum = 0;
                Desc.DepthMaximum = 1;
                GL.DepthFunc( DepthFunction.Lequal );
            }

            SetDepth( Desc.DepthMinimum, Desc.DepthMaximum );
        }

        public override void SetDepth( Single minimum, Single maximum )
        {
            GL.DepthRange( minimum, maximum );
        }

        public override void SetDrawBuffer( Boolean isFront )
        {
            GL.DrawBuffer( isFront ? DrawBufferMode.Front : DrawBufferMode.Back );
        }

        public override void Finish( )
        {
            GL.Finish( );
        }

        public override void SelectTexture( MTexTarget target )
        {
            if ( !Desc.SupportsMultiTexture )
                return;

            switch ( target )
            {
                case MTexTarget.TEXTURE0_SGIS:
                    GL.ActiveTexture( TextureUnit.Texture0 );
                    break;

                case MTexTarget.TEXTURE1_SGIS:
                    GL.ActiveTexture( TextureUnit.Texture1 );
                    break;

                default:
                    Utilities.Error( "GL_SelectTexture: Unknown target\n" );
                    break;
            }
        }

        public override void DisableMultitexture( )
        {
            if ( Desc.MultiTexturing )
            {
                GL.Disable( EnableCap.Texture2D );
                SelectTexture( MTexTarget.TEXTURE0_SGIS );
                Desc.MultiTexturing = false;
            }
        }

        public override void EnableMultitexture( )
        {
            if ( Desc.SupportsMultiTexture )
            {
                SelectTexture( MTexTarget.TEXTURE1_SGIS );
                GL.Enable( EnableCap.Texture2D );
                Desc.MultiTexturing = true;
            }
        }

        public override void ScreenShot( out String path )
        {
            base.ScreenShot( out path );

            var fs = FileSystem.OpenWrite( path, true );

            if ( fs == null )
            {
                ConsoleWrapper.Print( "SCR_ScreenShot_f: Couldn't create a file\n" );
                return;
            }

            using ( fs )
            {
                var width = Desc.ActualWidth;
                var height = Desc.ActualHeight;

                var pixels = new Byte[width * height * 3];

                GL.PixelStore( PixelStoreParameter.PackAlignment, 1 );

                GL.ReadPixels(
                    0,
                    0,
                    width,
                    height,
                    PixelFormat.Rgb,
                    PixelType.UnsignedByte,
                    pixels );

                FlipScreenShot( pixels, width, height );

                var writer = new ImageWriter( );

                writer.WriteJpg(
                    pixels,
                    width,
                    height,
                    ColorComponents.RedGreenBlue,
                    fs,
                    100 );
            }

            ConsoleWrapper.Print( "Wrote {0}\n", Path.GetFileName( path ) );
        }

        private static void FlipScreenShot( Byte[] pixels, Int32 width, Int32 height )
        {
            var stride = width * 3;
            var temp = new Byte[stride];

            for ( var y = 0; y < height / 2; y++ )
            {
                var top = y * stride;
                var bottom = ( height - y - 1 ) * stride;

                Buffer.BlockCopy( pixels, top, temp, 0, stride );
                Buffer.BlockCopy( pixels, bottom, pixels, top, stride );
                Buffer.BlockCopy( temp, 0, pixels, bottom, stride );
            }
        }

        /// <summary>
        /// R_RotateForEntity
        /// </summary>
        public override void RotateForEntity( Vector3 origin, Vector3 angles )
        {
            GL.Translate( origin.X, origin.Y, origin.Z );

            GL.Rotate( angles.Y, 0, 0, 1 );
            GL.Rotate( -angles.X, 0, 1, 0 );
            GL.Rotate( angles.Z, 1, 0, 0 );
        }

        /// <summary>
        /// R_BlendedRotateForEntity
        /// </summary>
        public override void BlendedRotateForEntity( Vector3 origin, Vector3 angles, Double realTime, ref Vector3 origin1, ref Vector3 origin2, ref Single translateStartTime, ref Vector3 angles1, ref Vector3 angles2, ref Single rotateStartTime )
        {
            var blend = 0f;
            var timepassed = realTime - translateStartTime;

            if ( translateStartTime == 0 || timepassed > 1 )
            {
                translateStartTime = ( Single ) realTime;

                origin1 = new Vector3( origin );
                origin2 = new Vector3( origin );
                blend = 0f;
            }

            if ( origin != origin2 )
            {
                translateStartTime = ( Single ) realTime;
                origin1 = new Vector3( origin2 );
                origin2 = new Vector3( origin );
                blend = 0;
            }
            else
            {
                blend = ( Single ) ( timepassed / 0.1f );

                if ( blend > 1 )
                    blend = 1;
            }

            var d = origin2 - origin1;

            GL.Translate( origin1.X + ( blend * d[0] ), origin1.Y + ( blend * d[1] ), origin1.Z + ( blend * d[2] ) );

            timepassed = realTime - rotateStartTime;

            if ( rotateStartTime == 0 || timepassed > 1 )
            {
                rotateStartTime = ( Single ) realTime;
                angles1 = new Vector3( angles );
                angles2 = new Vector3( angles );
            }

            if ( angles != angles2 )
            {
                rotateStartTime = ( Single ) realTime;
                angles1 = new Vector3( angles2 );
                angles2 = new Vector3( angles );
                blend = 0;
            }
            else
            {
                blend = ( Single ) ( timepassed / 0.1 );

                if ( blend > 1 )
                    blend = 1;
            }

            d = angles2 - angles1;

            for ( var i = 0; i < 3; i++ )
            {
                if ( d[i] > 180 )
                    d[i] -= 360;
                else if ( d[i] < -180 )
                    d[i] += 360;
            }

            GL.Rotate( angles1.Y + ( blend * d[1] ), 0, 0, 1 );
            GL.Rotate( -angles1.X + ( -blend * d[0] ), 0, 1, 0 );
            GL.Rotate( angles1.Z + ( blend * d[2] ), 1, 0, 0 );
        }
    }
}
