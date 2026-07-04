/// <copyright>
///
/// SharpQuakeEvolved changes by optimus-code, 2019-2023
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
using System.Drawing;
using SharpQuake.Factories.Rendering.UI;
using SharpQuake.Framework;
using SharpQuake.Framework.Factories.IO;
using SharpQuake.Framework.Factories.IO.WAD;
using SharpQuake.Framework.IO;
using SharpQuake.Framework.Logging;
using SharpQuake.Renderer.Textures;
using SharpQuake.Rendering;
using SharpQuake.Sys;

// gl_draw.c

namespace SharpQuake
{
    /// <summary>
    /// Draw_functions, GL_functions
    /// </summary>
    public class Drawer
    {
        public Int32 CurrentTexture = -1;

        public String LightMapFormat = "GL_RGBA";

        private readonly GLTexture_t[] _glTextures = new GLTexture_t[DrawDef.MAX_GLTEXTURES];

        public Byte[] _MenuPlayerPixels = new Byte[4096];
        public Int32 _MenuPlayerPixelWidth;
        public Int32 _MenuPlayerPixelHeight;

        public BasePicture BackgroundTile
        {
            get;
            private set;
        }

        private Renderer.Font CharSetFont
        {
            get;
            set;
        }

        private Renderer.TTFFont TTFFont
        {
            get;
            set;
        }

        private Renderer.TTFFont BigTTFFont
        {
            get;
            set;
        }

        private BaseTexture TranslateTexture
        {
            get;
            set;
        }

        // texture_extension_number = 1;
        // currenttexture = -1		// to avoid unnecessary texture sets
        private MTexTarget _OldTarget = MTexTarget.TEXTURE0_SGIS;

        // oldtarget
        private Int32[] _CntTextures = new Int32[2] { -1, -1 };

        // cnttextures
        private String CurrentFilter = "GL_LINEAR_MIPMAP_NEAREST";

        public Boolean IsInitialised
        {
            get;
            private set;
        }

        public Byte[] FontBuffer
        {
            get;
            private set;
        }

        public Int32 FontBufferOffset
        {
            get;
            private set;
        }

        private readonly IConsoleLogger _logger;
        private readonly CommandFactory _commands;
        private readonly ClientVariableFactory _cvars;
        private readonly WadFactory _wads;
        private readonly ElementFactory _elements;
        private readonly PictureFactory _pictures;
        private readonly Vid _video;

        public Drawer( IConsoleLogger logger, CommandFactory commands, ClientVariableFactory cvars, WadFactory wads, 
            ElementFactory elements, Vid video, PictureFactory pictures )
        {
            _logger = logger;
            _commands = commands;
            _cvars = cvars;
            _wads = wads;
            _elements = elements;
            _video = video;
            _pictures = pictures;
        }

        // Draw_Init
        public void Initialise( )
        {
            if ( Cvars.glNoBind == null )
            {
                Cvars.glNoBind = _cvars.Add( "gl_nobind", false );
                Cvars.glMaxSize = _cvars.Add( "gl_max_size", 8192 );
                Cvars.glPicMip = _cvars.Add( "gl_picmip", 0f );
                Cvars.TrueTypeFonts = _cvars.Add( "r_ttf", true );
            }

            // 3dfx can only handle 256 wide textures
            var renderer = _video.Device.Desc.Renderer;

            if ( renderer.Contains( "3dfx" ) || renderer.Contains( "Glide" ) )
                _cvars.Set( "gl_max_size", 256 );

            _commands.Add( "gl_texturemode", TextureMode_f );
            _commands.Add( "imagelist", Imagelist_f );

            InitialiseTypography( );            
            
            TranslateTexture = BaseTexture.FromDynamicBuffer( _video.Device, "_TranslateTexture", new ByteArraySegment( _MenuPlayerPixels ), _MenuPlayerPixelWidth, _MenuPlayerPixelHeight, false, true, "GL_LINEAR" );

            //
            // get the other pics we need
            //            
            BackgroundTile = BasePicture.FromWad( _video.Device, _wads.FromTexture( "backtile" ), "backtile", "GL_NEAREST" );

            IsInitialised = true;
        }

        private void InitialiseTypography()
        {
            // load the console background and the charset
            // by hand, because we need to write the version
            // string into the background before turning
            // it into a texture
            var concharsWad = _wads.FromTexture( "conchars" );
            var offset = concharsWad.GetLumpNameOffset( "conchars" );
            var draw_chars = concharsWad.Data; // draw_chars

            for ( var i = 0; i < 256 * 64; i++ )
            {
                if ( draw_chars[offset + i] == 0 )
                    draw_chars[offset + i] = 255;	// proper transparent color
            }

            var fontBuffer = new ByteArraySegment( draw_chars, offset );

            FontBuffer = draw_chars;
            FontBufferOffset = offset;

            // Temporarily set here
            BaseTexture.PicMip = Cvars.glPicMip.Get<Single>( );
            BaseTexture.MaxSize = Cvars.glMaxSize.Get<Int32>( );

            CharSetFont = new Renderer.Font( _video.Device, "charset" );
            CharSetFont.Initialise( fontBuffer );

            TTFFont = new Renderer.TTFFont( _video.Device, "Xolonium", 20 );

            var ttfFile = FileSystem.LoadFile( "Xolonium-Bold.ttf" );

            if ( ttfFile == null )
                ttfFile = System.IO.File.ReadAllBytes( @"C:\Windows\Fonts\Arial.ttf" );

            TTFFont.Initialise( new ByteArraySegment( ttfFile ) );

            BigTTFFont = new Renderer.TTFFont( _video.Device, "Big Xolonium", 52, 8 );
            BigTTFFont.Initialise( new ByteArraySegment( ttfFile ) );
        }

        public Int32 CharacterAdvance( Boolean forceCharset = false, Boolean isBigFont = false )
        {
            if ( !forceCharset && Cvars.TrueTypeFonts.Get<Boolean>( ) )
            {
                if ( isBigFont )
                    return BigTTFFont.CharacterAdvance( );
                else
                    return TTFFont.CharacterAdvance( );
            }
            else
                return CharSetFont.CharacterAdvance( );
        }

        public Int32 CharacterAdvanceHeight( Boolean forceCharset = false, Boolean isBigFont = false )
        {
            if ( !forceCharset && Cvars.TrueTypeFonts.Get<Boolean>( ) )
            {
                if ( isBigFont )
                    return BigTTFFont.CharacterAdvanceHeight( );
                else
                    return TTFFont.CharacterAdvanceHeight( );
            }
            else
                return CharSetFont.CharacterAdvanceHeight( );
        }

        // Draw_TileClear
        //
        // This repeats a 64*64 tile graphic to fill the screen around a sized down
        // refresh window.
        public void TileClear( Int32 x, Int32 y, Int32 w, Int32 h )
        {
            BackgroundTile.Source = new RectangleF( x / 64.0f, y / 64.0f, w / 64f, h / 64f );

            _video.Device.Graphics.DrawPicture( BackgroundTile, x, y, w, h );
        }
        
        // Draw_FadeScreen
        public void FadeScreen( )
        {
            _video.Device.Graphics.FadeScreen( );
            _elements.SetDirty( ElementFactory.HUD );
        }

        // Draw_Character
        //
        // Draws one 8*8 graphics character with 0 being transparent.
        // It can be clipped to the top of the screen to allow the console to be
        // smoothly scrolled off.
        // Vertex color modification has no effect currently
        public void DrawCharacter( Int32 x, Int32 y, Int32 num, Boolean forceCharset = false, System.Drawing.Color? color = null, Boolean isBigFont = false )
        {
            if ( !forceCharset && num >= 32 && Cvars.TrueTypeFonts.Get<Boolean>() )
            {
                if ( isBigFont )
                    BigTTFFont.DrawCharacter( x, y, num, color );
                else
                    TTFFont.DrawCharacter( x, y, num, color );
            }
            else
                CharSetFont.DrawCharacter( x, y, num, color );
        }

        public void DrawCharacterStretched( Int32 x, Int32 y, Int32 num, Int32 width )
        {
            CharSetFont.DrawCharacterStretched( x, y, num, width );
        }

        public Int32 MeasureCharacter( Char character, Boolean forceCharset = false, Boolean isBigFont = false )
        {
            if ( !forceCharset && character >= 32 && Cvars.TrueTypeFonts.Get<Boolean>( ) )
            {
                if ( isBigFont )
                    return BigTTFFont.Measure( character );
                else
                    return TTFFont.Measure( character );
            }
            else
                return CharSetFont.Measure( character );
        }

        public Int32 MeasureCharacterHeight( Char character, Boolean forceCharset = false, Boolean isBigFont = false )
        {
            if ( !forceCharset && character >= 32 && Cvars.TrueTypeFonts.Get<Boolean>( ) )
            {
                if ( isBigFont )
                    return BigTTFFont.MeasureHeight( character );
                else
                    return TTFFont.MeasureHeight( character );
            }
            else
                return CharSetFont.MeasureHeight( character );
        }

        public Int32 MeasureString( String text, Boolean forceCharset = false, Boolean isBigFont = false )
        {
            var width = 0;

            foreach ( var character in text )
            {
                width += CharacterAdvance( forceCharset, isBigFont ) + MeasureCharacter( character, forceCharset, isBigFont );
            }

            return width;
        }

        public UInt32[] GetCharacterBuffer( Char character, Boolean forceCharset = false, Boolean isBigFont = false )
        {
            if ( !forceCharset && character >= 32 && Cvars.TrueTypeFonts.Get<Boolean>( ) )
            {
                if ( isBigFont )
                    return BigTTFFont.GetCharacterBuffer( character );
                else
                    return TTFFont.GetCharacterBuffer( character );
            }
            else
                return CharSetFont.GetCharacterBuffer( character );
        }

        // Draw_String
        public void DrawString( Int32 x, Int32 y, String str, Boolean forceCharset = false, System.Drawing.Color? color = null, Boolean isBigFont = false )
        {
            if ( !forceCharset && Cvars.TrueTypeFonts.Get<Boolean>( ) )
            {
                if ( isBigFont )
                    BigTTFFont.Draw( x, y, str, color );
                else
                    TTFFont.Draw( x, y, str, color );
            }
            else
                CharSetFont.Draw( x, y, str, color );
        }

		public void DrawRichString( Int32 cx, Int32 cy, String str, Boolean forceCharset = false, Boolean isBigFont = false, Color? colour = null )
        {
            if ( !colour.HasValue && Cvars.TrueTypeFonts.Get<Boolean>( ) )
                colour = Colours.Quake;

            var defaultColour = colour;

            var xAdvance = cx;

            for ( var i = 0; i < str.Length; i++ )
            {
                var c = str[i];
                int color = -1;
                var inColorCode = str[i] == '^' && i + 1 < str.Length && int.TryParse( "" + str[i + 1], out color );

                if ( inColorCode && color >= 0 )
                {
                    colour = Colours.FromCode( color, defaultColour );
                    
                    i += 1;
                    continue;
                }

                DrawCharacter( xAdvance, cy, c, forceCharset, colour, isBigFont );
                xAdvance += CharacterAdvance( forceCharset, isBigFont ) + MeasureCharacter( ( Char ) c, forceCharset, isBigFont );
            }
        }

        public (Int32 X, Int32 Y) GetCharacterOffset( Char character, Boolean forceCharset = false, Boolean isBigFont = false )
        {
            if ( !forceCharset && Cvars.TrueTypeFonts.Get<Boolean>( ) )
            {
                if ( isBigFont )
                    return BigTTFFont.GetCharacterOffset( character );
                else
                    return TTFFont.GetCharacterOffset( character );
            }
            else
                return CharSetFont.GetCharacterOffset( character );
        }

        /// <summary>
        /// Draw_TransPicTranslate
        /// Only used for the player color selection menu
        /// </summary>
        public void TransPicTranslate( Int32 x, Int32 y, BasePicture pic, Byte[] translation )
        {
            _video.Device.Graphics.DrawTransTranslate( TranslateTexture, x, y, pic.Width, pic.Height, translation );
        }

        /// <summary>
        /// GL_SelectTexture
        /// </summary>
        public void SelectTexture( MTexTarget target )
        {
            if ( !_video.Device.Desc.SupportsMultiTexture )
                return;

            _video.Device.SelectTexture( target );

            if ( target == _OldTarget )
                return;

            _CntTextures[_OldTarget - MTexTarget.TEXTURE0_SGIS] = CurrentTexture;
            CurrentTexture = _CntTextures[target - MTexTarget.TEXTURE0_SGIS];
            _OldTarget = target;
        }

        /// <summary>
        /// Draw_TextureMode_f
        /// </summary>
        private void TextureMode_f( CommandMessage msg )
        {
            if ( msg.Parameters == null || msg.Parameters.Length == 0 )
            {
                foreach ( var textureFilter in _video.Device.TextureFilters )
                {
                    if ( CurrentFilter == textureFilter.Key )
                    {
                        _logger.Print( textureFilter.Key + '\n' );
                        return;
                    }
                }

                _logger.Print( "current filter is unknown???\n" );
                return;
            }

            BaseTextureFilter newFilter = null;

            foreach ( var textureFilter in _video.Device.TextureFilters )
            {
                if ( Utilities.SameText( textureFilter.Key, msg.Parameters[0] ) )
                {
                    newFilter = textureFilter.Value;
                    break;
                }
            }

            if ( newFilter == null )
            {
                _logger.Print( "bad filter name!\n" );
                return;
            }

            var count = 0;

            // change all the existing mipmap texture objects
            foreach ( var texture in BaseTexture.TexturePool )
            {
                var t = texture.Value;

                if ( t.Desc.HasMipMap )
                {
                    t.Desc.Filter = newFilter.Name;
                    t.Bind( );

                    _video.Device.SetTextureFilters( newFilter.Name );

                    count++;
                }
            }

            _logger.Print( $"Set {count} textures to {newFilter.Name}\n" );
            CurrentFilter = newFilter.Name;
        }

        private void Imagelist_f( CommandMessage msg )
        {
            Int16 textureCount = 0;

            foreach ( var glTexture in _glTextures )
            {
                if ( glTexture != null )
                {
                    _logger.Print( "{0} x {1}   {2}:{3}\n", glTexture.width, glTexture.height,
                    glTexture.owner, glTexture.identifier );
                    textureCount++;
                }
            }

            _logger.Print( "{0} textures currently loaded.\n", textureCount );
        }


        public void DrawFrame( Int32 x, Int32 y, Int32 width, Int32 height, Int32 scale = 1 )
        {
            DrawFrame( new Rectangle( x, y, width, height ), scale );
        }

        public void DrawFrame( Rectangle rect, Int32 scale = 1 )
        {
            var offset = 24; // Buffer to make sure it fits the bounds more accurately
            var x = rect.X - offset;
            var y = rect.Y - offset;
            var width = rect.Width + ( offset * 2 );
            var height = rect.Height + ( offset * 2 );

            var size = ( 8f * scale );
            var sizeMiddleX = ( 16f * scale );
            var intSize = ( Int32 ) ( 8f * scale );
            var doubleSize = intSize * 2;
            var innerWidth = ( width - doubleSize );
            var innerHeight = ( height - doubleSize );

            var topLeft = _pictures.Cache( "gfx/box_tl.lmp", "GL_NEAREST", ignoreAtlas: true );
            var middleLeft = _pictures.Cache( "gfx/box_ml.lmp", "GL_NEAREST", ignoreAtlas: true );
            var bottomLeft = _pictures.Cache( "gfx/box_bl.lmp", "GL_NEAREST", ignoreAtlas: true );

            var topMiddle = _pictures.Cache( "gfx/box_tm.lmp", "GL_NEAREST", ignoreAtlas: true );
            var middleTopCentre = _pictures.Cache( "gfx/box_mm.lmp", "GL_NEAREST", ignoreAtlas: true );
            var middleCentre = _pictures.Cache( "gfx/box_mm2.lmp", "GL_NEAREST", ignoreAtlas: true );
            var bottomMiddle = _pictures.Cache( "gfx/box_bm.lmp", "GL_NEAREST", ignoreAtlas: true );

            var topRight = _pictures.Cache( "gfx/box_tr.lmp", "GL_NEAREST", ignoreAtlas: true );
            var middleRight = _pictures.Cache( "gfx/box_mr.lmp", "GL_NEAREST", ignoreAtlas: true );
            var bottomRight = _pictures.Cache( "gfx/box_br.lmp", "GL_NEAREST", ignoreAtlas: true );

            // Top
            _video.Device.Graphics.DrawPicture( topLeft, new RectangleF( 0f, 0f, 1f, 1f ), new Rectangle( x, y, intSize, intSize ), hasAlpha: true );
            _video.Device.Graphics.DrawPicture( topMiddle, new RectangleF( 0f, 0f, innerWidth / size, 1f ), new Rectangle( x + intSize, y, innerWidth, intSize ), hasAlpha: true );
            _video.Device.Graphics.DrawPicture( topRight, new RectangleF( 0f, 0f, 1f, 1f ), new Rectangle( x + width - intSize, y, intSize, intSize ), hasAlpha: true );

            // Top - Shadow
            _video.Device.Graphics.DrawPicture( middleTopCentre, new RectangleF( 0f, 0f, innerWidth / sizeMiddleX, 1f ), new Rectangle( x + intSize, y + intSize, innerWidth, intSize ), hasAlpha: true );

            // Middle
            _video.Device.Graphics.DrawPicture( middleLeft, new RectangleF( 0f, 0f, 1f, innerHeight / size ), new Rectangle( x, y + intSize, intSize, innerHeight ), hasAlpha: true );

            // Middle fill
            _video.Device.Graphics.DrawPicture( middleCentre, new RectangleF( 0f, 0f, innerWidth / sizeMiddleX, ( innerHeight - intSize ) / size ), new Rectangle( x + intSize, y + doubleSize, innerWidth, innerHeight - intSize ), hasAlpha: true );

            _video.Device.Graphics.DrawPicture( middleRight, new RectangleF( 0f, 0f, 1f, innerHeight / size ), new Rectangle( x + width - intSize, y + intSize, intSize, innerHeight ), hasAlpha: true );

            // Bottom
            _video.Device.Graphics.DrawPicture( bottomLeft, new RectangleF( 0f, 0f, 1f, 1f ), new Rectangle( x, y + height - intSize, intSize, intSize ), hasAlpha: true );
            _video.Device.Graphics.DrawPicture( bottomMiddle, new RectangleF( 0f, 0f, width / size, 1f ), new Rectangle( x + intSize, y + height - intSize, innerWidth, intSize ), hasAlpha: true );
            _video.Device.Graphics.DrawPicture( bottomRight, new RectangleF( 0f, 0f, 1f, 1f ), new Rectangle( x + width - intSize, y + height - intSize, intSize, intSize ), hasAlpha: true );
        }
    }
}