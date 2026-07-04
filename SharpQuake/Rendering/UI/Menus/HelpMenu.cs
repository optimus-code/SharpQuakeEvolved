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
using SharpQuake.Desktop;
using SharpQuake.Factories.Rendering.UI;
using SharpQuake.Framework;

namespace SharpQuake.Rendering.UI
{
    public class HelpMenu : BaseMenu
    {
        private const Int32 NUM_HELP_PAGES = 6;

        private Int32 _Page;

        private readonly PictureFactory _pictures;

        public HelpMenu( IKeyboardInput keyboard, MenuFactory menus, PictureFactory pictures ) : base( "menu_help", keyboard, menus )
        {
            _pictures = pictures;
        }

        public override void Show( )
        {
            _Page = 0;
            base.Show( );
        }

        public override void KeyEvent( Int32 key )
        {
            switch ( key )
            {
                case KeysDef.K_ESCAPE:
                    _menus.Show( "menu_main" );
                    break;

                case KeysDef.K_UPARROW:
                case KeysDef.K_RIGHTARROW:
                    _menus.EnterSound = true;
                    if ( ++_Page >= NUM_HELP_PAGES )
                        _Page = 0;
                    break;

                case KeysDef.K_DOWNARROW:
                case KeysDef.K_LEFTARROW:
                    _menus.EnterSound = true;
                    if ( --_Page < 0 )
                        _Page = NUM_HELP_PAGES - 1;
                    break;
            }
        }

        public override void Draw( )
        {
            var scale = _menus.UIScale;
            var adorner = _menus.BuildAdorner( 0, 0, 0, width: 152 );

            var pic = _pictures.Cache( String.Format( "gfx/help{0}.lmp", _Page ), "GL_NEAREST" );
            _menus.DrawPic( adorner.MidPointX - ( ( pic.Width * _menus.UIScale )  / 2 ), adorner.MidPointY - ( ( pic.Height * scale ) / 2 ), pic, scale );
        }
    }
}
