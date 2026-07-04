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

using SharpQuake.Framework;
using SharpQuake.Renderer.Textures;
using System;
using System.Drawing;
using System.Linq;
using System.IO;
using SharpQuake.Rendering.UI.Menus;

namespace SharpQuake.Rendering
{
    public class TextPicture
    {
        public String FileName
        {
            get;
            private set;
        }

        private String Filter
        {
            get;
            set;
        }

        public BasePicture Picture
        {
            get;
            private set;
        }

        private BasePicture Reference
        {
            get;
            set;
        }

        public Int32 Width
        {
            get
            {
                return Reference.Width;
            }
        }

        public Int32 Height
        {
            get
            {
                return Reference.Height;
            }
        }

        private UInt32[] Buffer
        {
            get;
            set;
        }

        private Boolean ForceCharset
        {
            get;
            set;
        }

        private Boolean IsBigFont
        {
            get;
            set;
        }

        private Int32 Padding
        {
            get;
            set;
        }

        private readonly Vid _video;
        private readonly Drawer _drawer;
        
        public TextPicture( Drawer drawer, Vid video, String fileName, String filter = "GL_LINEAR_MIPMAP_NEAREST",
            Boolean forceCharset = false, Boolean isBigFont = false, Int32 padding = 4 )
        {
            _drawer = drawer;
            _video = video;

            FileName = fileName;
            Filter = filter;
            ForceCharset = forceCharset;
            IsBigFont = isBigFont;
            Padding = padding;

            Load( );
         }

        private void Load( )
        {
            Reference = BasePicture.FromFile( _video.Device, FileName, Filter, preservePixelBuffer: true );

            if ( Reference == null )
                Utilities.Error( "Couldn't load " + FileName );

            // For TTF fonts scale up the base texture by 4 to handle scaling vanilla fonts
            if ( !ForceCharset )
                ResizeReference( );

            Buffer = Reference.Texture.Buffer32.ToArray(); // Shortcut to create a copy
        }

        private void ResizeReference()
        {
            var scale = 4;
            var width = Reference.Width * scale;
            var height = Reference.Height * scale;
            var buffer = new UInt32[width * height];

            int targetIdx = 0;
            for ( int i = 0; i < height; ++i )
            {
                int iUnscaled = i / scale;
                for ( int j = 0; j < width; ++j )
                {
                    int jUnscaled = j / scale;
                    buffer[targetIdx++] = Reference.Texture.Buffer32[iUnscaled * Reference.Width + jUnscaled];
                }
            }

            Reference = BasePicture.FromBuffer( _video.Device, buffer, width, height, Path.GetFileNameWithoutExtension( FileName ) + "_resized", filter: Filter, preservePixelBuffer: true );
        }

        public BasePicture Build()
        {
            if ( Picture != null )
                return Picture;

            Picture = BasePicture.FromBuffer( _video.Device, Buffer, Reference.Width, Reference.Height, Path.GetFileNameWithoutExtension( FileName ), filter: Filter );

            return Picture;
        }

        private Int32 PositionToIndex( Int32 x, Int32 y )
        {
            return y * Reference.Width + x;
        }

        public void DrawCharacter( Int32 x, Int32 y, Char chr, Color? colour = null )
        {
            var character = chr;

            if ( ForceCharset && colour == Colours.Quake )
                character = ( Char ) ( character + 128 ); // As the built in Quake font has specific designs for colors use those

            var buffer = _drawer.GetCharacterBuffer( character, ForceCharset, IsBigFont );

            if ( buffer != null )
            {
                var width = _drawer.MeasureCharacter( character, ForceCharset, IsBigFont );
                var height = _drawer.MeasureCharacterHeight( character, ForceCharset, IsBigFont );

                // As we scale up the old font atm account for this
                if ( ForceCharset )
                {
                    width /= 4;
                    height /= 4;
                }

                for ( var curY = y; curY < y + height; curY++ )
                {
                    for ( var curX = x; curX < x + width; curX++ )
                    {
                        var sourceIndex = ( curY - y ) * width + ( curX - x );
                        var destIndex = PositionToIndex( curX, curY );

                        if ( sourceIndex >= buffer.Length || destIndex >= Buffer.Length )
                            continue;

                        var val = buffer[sourceIndex];
                        var c = Color.FromArgb( ( Int32 ) val );


                        if ( c.A > 0 )
                        {
                            if ( !ForceCharset && colour.HasValue )
                                c = colour.Value.ToBGR();

                            Buffer[destIndex] = ( UInt32 ) Color.FromArgb( 255, c ).ToArgb( );
                        }
                    }
                }
            }
        }

        public void DrawString( String text, Color? colour = null, TextAlignment alignment = TextAlignment.Left, VerticalAlighment verticalAlighment = VerticalAlighment.Top  )
        {
            var fullWidth = 0;
            var spaceWidth = _drawer.MeasureCharacter( ' ', ForceCharset, IsBigFont );
            var characterHeight = _drawer.CharacterAdvanceHeight( ForceCharset, IsBigFont );
            var characterAdvance = _drawer.CharacterAdvance( ForceCharset, IsBigFont );

            // As we scale up the old font atm account for this
            if ( ForceCharset )
            {
                spaceWidth /= 4;
                characterHeight /= 4;
            }

            // Calculate width here as we have less singificant spacing for texture text - due to some weird bugs
            foreach ( var character in text )
            {
                if ( character == ' ' )
                {
                    fullWidth += ( spaceWidth / 4 );
                    continue;
                }

                var characterWidth = _drawer.MeasureCharacter( character, ForceCharset, IsBigFont );

                if ( ForceCharset )
                    characterWidth /= 4;

                fullWidth += characterAdvance + characterWidth;
            }

            var xAdvance = alignment == TextAlignment.Right ? Width - fullWidth - Padding : alignment == TextAlignment.Centre ? ( Width / 2 ) - ( fullWidth / 2 ) : Padding;
            var yAdvance = verticalAlighment == VerticalAlighment.Bottom ? Height - characterHeight - Padding : verticalAlighment == VerticalAlighment.Middle ? ( Height / 2 ) - ( characterHeight / 2 ) : Padding;

            foreach ( var character in text )
            {
                if ( character == ' ' )
                {
                    xAdvance += ( spaceWidth / 4 );
                    continue;
                }

                var offset = _drawer.GetCharacterOffset( character, ForceCharset, IsBigFont );

                if ( ForceCharset )
                    offset = (offset.X / 4, offset.Y / 4);

                DrawCharacter( xAdvance + offset.X, ( Int32 ) ( yAdvance - offset.Y + ( ForceCharset ? characterHeight : ( characterHeight / 1.5f ) ) ), character, colour );

                var characterWidth = _drawer.MeasureCharacter( character, ForceCharset, IsBigFont );

                if ( ForceCharset )
                    characterWidth /= 4;

                xAdvance += characterAdvance + characterWidth;
            }
        }
    }

    public enum VerticalAlighment
    {
        Top,
        Middle,
        Bottom
    }
}
