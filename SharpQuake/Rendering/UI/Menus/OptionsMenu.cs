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
using SharpQuake.Framework.Factories.IO;
using SharpQuake.Rendering.UI.Menus;
using SharpQuake.Sys;

namespace SharpQuake.Rendering.UI
{
    public class OptionsMenu : BaseMenu
    {
        private const Int32 OPTIONS_ITEMS = 13;

        //private float _BgmVolumeCoeff = 0.1f;

        private readonly client _client;
        private readonly View _view;
        private readonly CommandFactory _commands;
        private readonly ClientVariableFactory _cvars;
        private readonly PictureFactory _pictures;
        private readonly snd _sound;
        private readonly Scr _screen;
        public OptionsMenu( IKeyboardInput keyboard, CommandFactory commands, ClientVariableFactory cvars,
            PictureFactory pictures, MenuFactory menus, client client, View view, snd sound,
            Scr screen ) : base( "menu_options", keyboard, menus )
        {
            _client = client;
            _view = view;
            _commands = commands;
            _sound = sound;
            _cvars = cvars;
            _pictures = pictures;
            _screen = screen;
        }

        public override void Show( )
        {
            /*if( sys.IsWindows )  fix cd audio first
             {
                 _BgmVolumeCoeff = 1.0f;
             }*/

            if ( Cursor > OPTIONS_ITEMS - 1 )
                Cursor = 0;

            base.Show( );
        }

        public override void KeyEvent( Int32 key )
        {
            switch ( key )
            {
                case KeysDef.K_ESCAPE:
                    _menus.Show( "menu_main" );
                    break;

                case KeysDef.K_ENTER:
                    _menus.EnterSound = true;
                    switch ( Cursor )
                    {
                        case 0:
                            _menus.Show( "menu_keys" );
                            break;

                        case 1:
                            _menus.CurrentMenu.Hide( );
                            _commands.Buffer.Append( "toggleconsole\n" );
                            break;

                        case 2:
                            _commands.Buffer.Append( "exec default.cfg\n" );
                            break;

                        case 12:
                            _menus.Show( "menu_video" );
                            break;

                        default:
                            AdjustSliders( 1 );
                            break;
                    }
                    return;

                case KeysDef.K_UPARROW:
                    _sound.LocalSound( "misc/menu1.wav" );
                    Cursor--;
                    if ( Cursor < 0 )
                        Cursor = OPTIONS_ITEMS - 1;
                    break;

                case KeysDef.K_DOWNARROW:
                    _sound.LocalSound( "misc/menu1.wav" );
                    Cursor++;
                    if ( Cursor >= OPTIONS_ITEMS )
                        Cursor = 0;
                    break;

                case KeysDef.K_LEFTARROW:
                    AdjustSliders( -1 );
                    break;

                case KeysDef.K_RIGHTARROW:
                    AdjustSliders( 1 );
                    break;
            }

            /*if( Cursor == 12 && VideoMenu == null )
            {
                if( key == KeysDef.K_UPARROW )
                    Cursor = 11;
                else
                    Cursor = 0;
            }*/

            if ( Cursor == 12 )
            {
                if ( key == KeysDef.K_UPARROW )
                    Cursor = 11;
                else
                    Cursor = 0;
            }

            /*#if _WIN32
                        if ((optionsCursor == 13) && (modestate != MS_WINDOWED))
                        {
                            if (k == K_UPARROW)
                                optionsCursor = 12;
                            else
                                optionsCursor = 0;
                        }
            #endif*/
        }

        private void DrawPlaque( MenuAdorner adorner )
        {
            var scale = _menus.UIScale;

            //_menus.DrawTransPic( 16 * scale, 4 * scale, _pictures.Cache( "gfx/qplaque.lmp", "GL_NEAREST" ), scale: scale );
            var logoPic = _pictures.Cache( "gfx/qplaque.lmp", "GL_NEAREST" );
            _menus.DrawTransPic( adorner.LeftPoint - ( logoPic.Width * _menus.UIScale ), adorner.MidPointY - ( ( logoPic.Height * scale ) / 2 ), logoPic, scale: scale );

            var p = _pictures.Cache( "gfx/p_option.lmp", "GL_NEAREST" );
            _menus.DrawPic( adorner.MidPointX - ( ( p.Width * scale )  / 2 ), 4 * scale, p, scale: scale );
        }

        private void DrawSound( MenuAdorner adorner )
        {
            var scale = _menus.UIScale;

            //_menus.Print( 16 * scale, 80 * scale, "       CD Music Volume", scale: scale );
            //var r = _sound.BgmVolume;
            //_menus.DrawSlider( 220 * scale, 80 * scale, r );

            //_menus.Print( 16 * scale, 88 * scale, "          Sound Volume", scale: scale );
            //r = _sound.Volume;
            //_menus.DrawSlider( 220 * scale, 88 * scale, r );
            var r = _sound.BgmVolume;
            adorner.Slider( "CD Music Volume", r );

            r = _sound.Volume;
            adorner.Slider( "Sound Volume", r );
        }

        private void DrawMovementControls( MenuAdorner adorner )
        {
            var scale = _menus.UIScale;

            //_menus.Print( 16 * scale, 72 * scale, "           Mouse Speed", scale: scale );
            //var r = ( _client.Sensitivity - 1 ) / 10;
            //_menus.DrawSlider( 220 * scale, 72 * scale, r );

            //_menus.Print( 16 * scale, 96 * scale, "            Always Run", scale: scale );
            //_menus.DrawCheckbox( 220 * scale, 96 * scale, _client.ForwardSpeed > 200 );

            //_menus.Print( 16 * scale, 104 * scale, "          Invert Mouse", scale: scale );
            //_menus.DrawCheckbox( 220 * scale, 104 * scale, _client.MPitch < 0 );

            //_menus.Print( 16 * scale, 112 * scale, "            Lookspring", scale: scale );
            //_menus.DrawCheckbox( 220 * scale, 112 * scale, _client.LookSpring );

            //_menus.Print( 16 * scale, 120 * scale, "            Lookstrafe", scale: scale );
            //_menus.DrawCheckbox( 220 * scale, 120 * scale, _client.LookStrafe );

            var r = ( _client.Sensitivity - 1 ) / 10;
            adorner.Slider( "Mouse Speed", r );

            adorner.CheckBox( "Always Run", _client.ForwardSpeed > 200 );
            adorner.CheckBox( "Invert Mouse", _client.MPitch < 0 );
            adorner.CheckBox( "Lookspring", _client.LookSpring );
            adorner.CheckBox( "Lookstrafe", _client.LookStrafe );
        }

        private void DrawScreenSettings( MenuAdorner adorner )
        {
            var scale = _menus.UIScale;

            //_menus.Print( 16 * scale, 56 * scale, "           Screen size", scale: scale );
            //
            //_menus.DrawSlider( 220 * scale, 56 * scale, r );

            //_menus.Print( 16 * scale, 64 * scale, "            Brightness", scale: scale );
            //r = ( 1.0f - _view.Gamma ) / 0.5f;
            //_menus.DrawSlider( 220 * scale, 64 * scale, r );

            var r = ( _screen.ViewSize.Get<Single>( ) - 30 ) / ( 120 - 30 );
            adorner.Slider( "Screen size", r );

            r = ( 1.0f - _view.Gamma ) / 0.5f;
            adorner.Slider( "Brightness", r );
        }

        public override void Draw( )
        {
            var adorner = _menus.BuildAdorner( 13, 0, 0, width: 152 );

            DrawPlaque( adorner );

            adorner.Print( "Customize controls" );
            adorner.Print( "Go to console" );
            adorner.Print( "Reset to defaults" );
            adorner.NewLine( );

            var scale = _menus.UIScale;
            //_menus.Print( 16 * scale, 32 * scale, "    Customize controls", scale: scale );
            //_menus.Print( 16 * scale, 40 * scale, "         Go to console", scale: scale );
            //_menus.Print( 16 * scale, 48 * scale, "     Reset to defaults", scale: scale );

            DrawScreenSettings( adorner );
            DrawSound( adorner );
            DrawMovementControls( adorner );

            /*if( VideoMenu != null )
                Host.Menu.Print( 16, 128, "         Video Options" );*/

#if _WIN32
	if (modestate == MS_WINDOWED)
	{
		Host.Menu.Print (16, 136, "             Use Mouse");
		Host.Menu.DrawCheckbox (220, 136, _windowed_mouse.value);
	}
#endif

            // cursor
            _menus.DrawCharacter( adorner.MidPointX - ( adorner.Padding / 2 ), adorner.LineY( Cursor ), 12 + ( ( Int32 ) ( Time.Absolute * 4 ) & 1 ), scale: scale );
        }

        /// <summary>
        /// M_AdjustSliders
        /// </summary>
        private void AdjustSliders( Int32 dir )
        {
            _sound.LocalSound( "misc/menu3.wav" );
            Single value;

            switch ( Cursor )
            {
                case 3:	// screen size
                    value = _screen.ViewSize.Get<Single>( ) + dir * 10;
                    if ( value < 30 )
                        value = 30;
                    if ( value > 120 )
                        value = 120;
                    _cvars.Set( "viewsize", value );
                    break;

                case 4:	// gamma
                    value = _view.Gamma - dir * 0.05f;
                    if ( value < 0.5 )
                        value = 0.5f;
                    if ( value > 1 )
                        value = 1;
                    _cvars.Set( "gamma", value );
                    break;

                case 7:// 5:	// mouse speed - sfx volume
                    value = _client.Sensitivity + dir * 0.5f;
                    if ( value < 1 )
                        value = 1;
                    if ( value > 11 )
                        value = 11;
                    _cvars.Set( "sensitivity", value );
                    break;

                case 5://6:	// music volume - mouse speed
                    value = _sound.BgmVolume + dir * 0.1f; ///_BgmVolumeCoeff;
                    if ( value < 0 )
                        value = 0;
                    if ( value > 1 )
                        value = 1;
                    _cvars.Set( "bgmvolume", value );
                    break;

                case 6:// 7:	// sfx volume - music volume
                    value = _sound.Volume + dir * 0.1f;
                    if ( value < 0 )
                        value = 0;
                    if ( value > 1 )
                        value = 1;
                    _cvars.Set( "volume", value );
                    break;

                case 8:	// allways run
                    if ( _client.ForwardSpeed > 200 )
                    {
                        _cvars.Set( "cl_forwardspeed", 200f );
                        _cvars.Set( "cl_backspeed", 200f );
                    }
                    else
                    {
                        _cvars.Set( "cl_forwardspeed", 400f );
                        _cvars.Set( "cl_backspeed", 400f );
                    }
                    break;

                case 9:	// invert mouse
                    _cvars.Set( "m_pitch", -_client.MPitch );
                    break;

                case 10:	// lookspring
                    _cvars.Set( "lookspring", !_client.LookSpring );
                    break;

                case 11:	// lookstrafe
                    _cvars.Set( "lookstrafe", !_client.LookStrafe );
                    break;

#if _WIN32
	        case 13:	// _windowed_mouse
		        Cvar_SetValue ("_windowed_mouse", !_windowed_mouse.value);
		        break;
#endif
            }
        }
    }
}
