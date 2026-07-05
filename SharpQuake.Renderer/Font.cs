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
using System.Drawing;
using SharpQuake.Framework;
using SharpQuake.Renderer.Textures;

namespace SharpQuake.Renderer
{
    public class Font : IDisposable
    {
        public const Int32 FONT_SIZE_PIXELS = 8;

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

        public Font( BaseDevice device, String name )
        {
            Device = device;
            Name = name;
        }

        public virtual void Initialise( ByteArraySegment buffer )
        {
            Texture = BaseTexture.FromBuffer( Device, Name, buffer, 128, 128, false, true, filter: "GL_NEAREST" );
        }

        public virtual Int32 Measure( UInt32 character, int scale = 4 )
        {
            return 8 * scale;
        }

        public virtual Int32 MeasureHeight( UInt32 character, int scale = 4 )
        {
            return 8 * scale;
        }

        public virtual Int32 Measure( String str, int scale = 4 )
        {
            return str.Length * ( 8 * scale );
        }

        // Draw_String
        public virtual void Draw( Int32 x, Int32 y, String str, Color? color = null, int scale = 4 )
        {
            var xAdvance = x;
            for ( var i = 0; i < str.Length; i++ )
            {
                DrawCharacter( xAdvance, y, str[i], color, scale );
                xAdvance += CharacterAdvance( ) + Measure( str[i], scale );
            }
        }

        // Draw_Character
        //
        // Draws one 8*8 graphics character with 0 being transparent.
        // It can be clipped to the top of the screen to allow the console to be
        // smoothly scrolled off.
        // Vertex color modification has no effect currently
        public virtual void DrawCharacter( Int32 x, Int32 y, Int32 num, Color? colour = null, int scale = 4 )
        {
            if ( num == 32 )
                return;		// space

            num &= 255;

            if ( y <= -FONT_SIZE_PIXELS )
                return;			// totally off screen

            var row = num >> 4;
            var col = num & 15;

            var size = 0.0625f;
            var frow = row * 0.0625f;
            var fcol = col * 0.0625f;

            var cW = Measure( ( UInt32 ) num, scale );
            var cH = MeasureHeight( ( UInt32 ) num, scale );

            Device.Graphics.DrawTexture2D( Texture,
                   new RectangleF( fcol, frow, size, size ), new Rectangle( x * scale, y * scale, cW * scale, cH * scale ), colour );
        }

        public virtual void DrawCharacterStretched( Int32 x, Int32 y, Int32 num, Int32 width, Color? colour = null )
        {
            if ( num == 32 )
                return;		// space

            num &= 255;

            if ( y <= -FONT_SIZE_PIXELS )
                return;			// totally off screen

            var row = num >> 4;
            var col = num & 15;

            var size = 0.0625f;
            var frow = row * 0.0625f;
            var fcol = col * 0.0625f;

            var cH = MeasureHeight( ( UInt32 ) num );

            Device.Graphics.DrawTexture2D( Texture,
                   new RectangleF( fcol, frow, size, size ), new Rectangle( x, y, width, cH ), colour );
        }

        public virtual UInt32[] GetCharacterBuffer( Int32 num )
        {
            if ( num == 32 )
                return null;        // space

            num &= 255;
            

            var row = num >> 4;
            var col = num & 15;
            var size = 0.0625f;
            var frow = row * size;
            var fcol = col * size;

            var dataY = ( Int32 ) ( Texture.Desc.Height * frow );
            var dataX = ( Int32 ) ( Texture.Desc.Width * fcol );

            var dataWidth = 8;
            var dataHeight = 8;

            var buffer = Texture.Buffer32;
            var result = new UInt32[dataWidth * dataHeight];

            for ( var y = dataY; y < dataY + dataHeight; y++ )
            {
                for ( var x = dataX; x < dataX + dataWidth; x++ )
                {
                    var sourceIndex = y * Texture.Desc.Width + x;
                    var destIndex = ( y - dataY ) * dataWidth + ( x - dataX );

                    if ( sourceIndex >= buffer.Length || destIndex >= result.Length )
                        continue;
                    
                    result[destIndex] = buffer[sourceIndex];
                }
            }

            return result;
        }

        public virtual (Int32 X, Int32 Y) GetCharacterOffset( Int32 num )
        {
            return (0, 8 * 4);
        }

        public Int32 CharacterAdvance( )
        {
            return 0;
        }

        public Int32 CharacterAdvanceHeight( )
        {
            var height = MeasureHeight( 'T' );

            return height;
        }

        public virtual void Dispose( )
        {
        }
    }
}
