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
using System.Drawing;
using SharpFont;
using System.IO;
using SharpQuake.Framework;
using SharpQuake.Renderer.Textures;

namespace SharpQuake.Renderer
{
    public class TTFFont : IDisposable
    {
        public BaseDevice Device
        {
            get;
            private set;
        }

        public String Name
        {
            get;
            private set;
        }

        public BaseTexture Texture
        {
            get;
            private set;
        }

        public Int32 FontSize
        {
            get;
            private set;
        }

        private Int32 LetterSpacing
        {
            get;
            set;
        } = 1;

        private const Int32 FONT_SIZE = 29;
        private const UInt32 SPACE_CHARACTER = 32;
        private const UInt32 ASCII_CHARACTER_COUNT = 128;

        Dictionary<uint, TTFCharacter> _characters = new Dictionary<uint, TTFCharacter>( );

        public TTFFont( 
            BaseDevice device, 
            String name, 
            Int32 fontSize = FONT_SIZE, 
            Int32 letterSpacing = 1 )
        {
            Device = device;
            Name = name;
            FontSize = fontSize;
            LetterSpacing = letterSpacing;
        }

        public virtual void Initialise( ByteArraySegment buffer )
        {
            using ( var lib = new Library( ) )
            using ( var ms = new MemoryStream( buffer.Data ) )
            using ( var face = new Face( lib, ms.ToArray( ), 0 ) )
            {
                var fontSize = FontSize;

                face.SetPixelSizes( 0, ( uint ) fontSize );

                var cellSize = fontSize;
                var textureSize = 1024;
                var gridRows = textureSize / cellSize;
                var gridColumns = gridRows;
                var bytesPerPixel = 4;
                var pixels = new Byte[( textureSize * textureSize ) * bytesPerPixel];

                UInt32 character = 0;

                // Load first 128 characters of ASCII set.
                // Keep the original cell layout: Quake-ish callers depend on these byte character assumptions.
                for ( var y = 0; y < gridRows; y++ )
                {
                    for ( var x = 0; x < gridColumns; x++, character++ )
                    {
                        if ( character >= ASCII_CHARACTER_COUNT )
                            break;

                        try
                        {
                            face.LoadChar( character, LoadFlags.Render, LoadTarget.Normal );

                            GlyphSlot glyph = face.Glyph;
                            FTBitmap bitmap = glyph.Bitmap;

                            var absX = x * cellSize;
                            var absY = y * cellSize;
                            var glyphBuffer = bitmap.BufferData;
                            var advanceX = RoundFTAdvance( glyph.Advance.X.Value );

                            var data = new TTFCharacter
                            {
                                X = absX,
                                Y = absY,
                                OffsetX = glyph.BitmapLeft,
                                OffsetY = glyph.BitmapTop,
                                AdvanceX = advanceX
                            };

                            if ( character == SPACE_CHARACTER )
                            {
                                data.Width = CalculateStoredSpaceWidth( advanceX, fontSize );
                                data.Height = 0;
                                _characters[character] = data;
                                continue;
                            }

                            var copyWidth = Math.Min( bitmap.Width, cellSize );
                            var copyHeight = Math.Min( bitmap.Rows, cellSize );

                            CopyGlyphBitmapToAtlas( bitmap, glyphBuffer, pixels, textureSize, absX, absY, copyWidth, copyHeight );

                            data.Width = copyWidth;
                            data.Height = copyHeight;

                            _characters[character] = data;
                        }
                        catch ( Exception ex )
                        {
                            //Utilities.Error( ex.ToString( ) );
                        }
                    }
                }

                var uintData = new UInt32[pixels.Length / 4];
                Buffer.BlockCopy( pixels, 0, uintData, 0, pixels.Length );

                Texture = BaseTexture.FromBuffer( Device, Name + "_Tex", uintData, textureSize, textureSize, false, true, "GL_LINEAR", preservePixelBuffer: true );
            }
        }

        private Int32 CalculateStoredSpaceWidth( Int32 advanceX, Int32 fontSize )
        {
            var actualSpaceAdvance = advanceX > 0 ? advanceX : Math.Max( 1, fontSize / 4 );

            return Math.Max( 1, actualSpaceAdvance - LetterSpacing );
        }

        /// <summary>
        /// FreeType advances are 26.6 fixed-point values. Round to the nearest pixel.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static Int32 RoundFTAdvance( Int64 value )
        {
            return ( Int32 ) ( ( value + 32 ) >> 6 );
        }

        private static UInt32 NormalizeLegacyQuakeCharacter( UInt32 character )
        {
            // Preserve the original SharpQuakeEvolved hack:
            // high-bit printable chars map back to the base ASCII glyph.
            if ( character > 128 && character - 128 > 32 )
                character -= 128;

            return character;
        }

        private static void CopyGlyphBitmapToAtlas( FTBitmap bitmap, Byte[] glyphBuffer, Byte[] pixels, Int32 textureSize, Int32 absX, Int32 absY, Int32 copyWidth, Int32 copyHeight )
        {
            if ( bitmap.Width <= 0 || bitmap.Rows <= 0 || glyphBuffer == null || glyphBuffer.Length == 0 )
                return;

            var glyphPitch = Math.Abs( bitmap.Pitch );

            if ( glyphPitch == 0 )
                glyphPitch = bitmap.Width;

            for ( var glyphY = 0; glyphY < copyHeight; glyphY++ )
            {
                var glyphRow = bitmap.Pitch >= 0
                    ? glyphY * glyphPitch
                    : ( bitmap.Rows - 1 - glyphY ) * glyphPitch;

                for ( var glyphX = 0; glyphX < copyWidth; glyphX++ )
                {
                    var val = ReadGlyphAlpha( bitmap, glyphBuffer, glyphRow, glyphX );
                    var pixelsI = ( ( ( absY + glyphY ) * textureSize ) + ( absX + glyphX ) ) * 4;

                    if ( pixelsI < 0 || pixelsI + 3 >= pixels.Length )
                        continue;

                    pixels[pixelsI] = 255;
                    pixels[pixelsI + 1] = 255;
                    pixels[pixelsI + 2] = 255;
                    pixels[pixelsI + 3] = val;
                }
            }
        }

        private static Byte ReadGlyphAlpha( FTBitmap bitmap, Byte[] glyphBuffer, Int32 glyphRow, Int32 glyphX )
        {
            if ( bitmap.PixelMode == PixelMode.Mono )
            {
                var glyphI = glyphRow + ( glyphX >> 3 );

                if ( glyphI < 0 || glyphI >= glyphBuffer.Length )
                    return 0;

                return ( glyphBuffer[glyphI] & ( 0x80 >> ( glyphX & 7 ) ) ) != 0 ? ( Byte ) 255 : ( Byte ) 0;
            }

            if ( bitmap.PixelMode == PixelMode.Gray2 )
            {
                var glyphI = glyphRow + ( glyphX >> 2 );

                if ( glyphI < 0 || glyphI >= glyphBuffer.Length )
                    return 0;

                var shift = 6 - ( ( glyphX & 3 ) * 2 );
                return ( Byte ) ( ( ( glyphBuffer[glyphI] >> shift ) & 0x03 ) * 85 );
            }

            if ( bitmap.PixelMode == PixelMode.Gray4 )
            {
                var glyphI = glyphRow + ( glyphX >> 1 );

                if ( glyphI < 0 || glyphI >= glyphBuffer.Length )
                    return 0;

                var shift = ( glyphX & 1 ) == 0 ? 4 : 0;
                return ( Byte ) ( ( ( glyphBuffer[glyphI] >> shift ) & 0x0F ) * 17 );
            }

            var grayIndex = glyphRow + glyphX;

            if ( grayIndex < 0 || grayIndex >= glyphBuffer.Length )
                return 0;

            return glyphBuffer[grayIndex];
        }

        public Int32 CharacterAdvance( )
        {
            return LetterSpacing;
        }

        public virtual Int32 Measure( UInt32 character )
        {
            var c = NormalizeLegacyQuakeCharacter( character );

            if ( c == SPACE_CHARACTER )
            {
                TTFCharacter spaceData;

                if ( _characters.TryGetValue( SPACE_CHARACTER, out spaceData ) )
                    return spaceData.Width;

                return Math.Max( 1, ( FontSize / 4 ) - LetterSpacing );
            }

            TTFCharacter data;

            if ( c >= ASCII_CHARACTER_COUNT || !_characters.TryGetValue( c, out data ) )
                return MeasureFallbackWidth( );

            return data.Width;
        }

        private Int32 MeasureFallbackWidth( )
        {
            TTFCharacter data;

            if ( _characters.TryGetValue( ( UInt32 ) 'e', out data ) )
                return data.Width;

            if ( _characters.TryGetValue( ( UInt32 ) 'T', out data ) )
                return data.Width;

            return Math.Max( 1, FontSize / 2 );
        }

        public virtual Int32 MeasureHeight( UInt32 character )
        {
            var c = NormalizeLegacyQuakeCharacter( character );

            if ( c == SPACE_CHARACTER )
                return MeasureHeight( 'T' );

            TTFCharacter data;

            if ( c >= ASCII_CHARACTER_COUNT || !_characters.TryGetValue( c, out data ) )
                return MeasureHeight( 'T' );

            return data.Height;
        }

        public Int32 CharacterAdvanceHeight( )
        {
            var height = MeasureHeight( 'T' );

            return 16 + height;
        }

        public virtual Int32 Measure( String str )
        {
            var width = 0;

            if ( String.IsNullOrEmpty( str ) )
                return width;

            for ( var i = 0; i < str.Length; i++ )
            {
                var c = str[i];
                width += Measure( c );
            }

            return width;
        }

        // Draw_String
        public virtual void Draw( Int32 x, Int32 y, String str, Color? color = null )
        {
            if ( String.IsNullOrEmpty( str ) )
                return;

            var xAdvance = x;

            for ( var i = 0; i < str.Length; i++ )
            {
                DrawCharacter( xAdvance, y, str[i], color );
                xAdvance += CharacterAdvance( ) + Measure( str[i] );
            }
        }

        // Draw_Character
        //
        // Draws one 8*8 graphics character with 0 being transparent.
        // It can be clipped to the top of the screen to allow the console to be
        // smoothly scrolled off.
        // Vertex color modification has no effect currently
        public virtual void DrawCharacter( Int32 x, Int32 y, Int32 num, Color? colour = null )
        {
            if ( num == 32 )
                return;     // space

            //num &= 255;

            if ( y <= -8 )
                return;         // totally off screen

            var row = num >> 4;
            var col = num & 15;

            //var frow = row * 0.0625f;
            //var fcol = col * 0.0625f;
            //var size = 0.0625f;

            var nnum = num;

            if ( nnum > 128 && nnum - 128 > 32 )
                nnum -= 128;
            //if ( nnum > 128 )
            //{
            //    return;
            //}
            // TODO - CHANGE, this is a hack to support the multiple text modes the game uses
            //if ( nnum > 128 && nnum - 128 >= 0 )
            //{
            //    nnum = num - 128;
            //}

            if ( nnum >= 128 ) // We currently dont do anything higher!
                return;

            if ( nnum == 32 || !_characters.ContainsKey( ( UInt32 ) nnum ) )
                return;     // space

            var data = _characters[( UInt32 ) nnum];
            var fcol = ( data.X ) / ( float ) Texture.Desc.Width;
            var frow = ( data.Y ) / ( float ) Texture.Desc.Height;
            var sizeX = ( data.Width / ( float ) Texture.Desc.Width );
            var sizeY = ( data.Height / ( float ) Texture.Desc.Height );

            Device.Graphics.DrawTexture2D( Texture,
                   new RectangleF( fcol, frow, sizeX, sizeY ), new Rectangle( x + ( data.OffsetX ), y - ( data.OffsetY ) + FontSize, data.Width, data.Height ), colour );
        }

        public virtual (Int32 X, Int32 Y) GetCharacterOffset( Int32 num )
        {
            var nnum = num;

            if ( nnum >= 128 ) // We currently dont do anything higher!
                return (0, 0);

            if ( nnum == 32 || !_characters.ContainsKey( ( UInt32 ) nnum ) )
                return (0, 0);

            var data = _characters[( UInt32 ) nnum];

            return (data.OffsetX, data.OffsetY);
        }

        public virtual UInt32[] GetCharacterBuffer( Int32 num )
        {
            if ( num == 32 )
                return null;        // space

            var nnum = num;

            if ( nnum > 128 && nnum - 128 > 32 )
                nnum -= 128;

            if ( nnum >= 128 ) // We currently dont do anything higher!
                return null;

            if ( nnum == 32 || !_characters.ContainsKey( ( UInt32 ) nnum ) )
                return null;        // space

            var data = _characters[( UInt32 ) nnum];
            var buffer = Texture.Buffer32;
            var result = new UInt32[data.Width * data.Height];

            for ( var y = data.Y; y < data.Y + data.Height; y++ )
            {
                for ( var x = data.X; x < data.X + data.Width; x++ )
                {
                    var sourceIndex = y * Texture.Desc.Width + x;
                    var destIndex = ( y - data.Y ) * data.Width + ( x - data.X );
                    result[destIndex] = buffer[sourceIndex];
                }
            }

            return result;
        }

        public virtual void Dispose( )
        {
        }
    }

    public struct TTFCharacter
    {
        public int X
        {
            get;
            set;
        }

        public int Y
        {
            get;
            set;
        }

        public int OffsetX
        {
            get;
            set;
        }

        public int OffsetY
        {
            get;
            set;
        }

        public int Width
        {
            get;
            set;
        }

        public int Height
        {
            get;
            set;
        }

        public int AdvanceX
        {
            get;
            set;
        }
    }
}
