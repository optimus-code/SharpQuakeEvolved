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

using SharpQuake.Desktop;
using SharpQuake.Factories.Rendering.UI;
using SharpQuake.Framework;
using SharpQuake.Framework.Factories.IO;
using SharpQuake.Rendering.UI.Menus;
using SharpQuake.Sys;
using System;

namespace SharpQuake.Rendering.UI
{
	/// <summary>
	/// M_Menu_LanConfig_functions
	/// </summary>
	public class LanConfigMenu : BaseMenu
	{
		public Boolean JoiningGame
		{
			get
			{
				return _menus.Get( "menu_multiplayer" ).Cursor == 0;
			}
		}

		public Boolean StartingGame
		{
			get
			{
				return _menus.Get( "menu_multiplayer" ).Cursor == 1;
			}
		}

		private const Int32 NUM_LANCONFIG_CMDS = 3;

		private static readonly Int32[] CursorTable = new Int32[] { 72, 92, 124 };

		private Int32 _Port;
		private String _PortName;
		private String _JoinName;

		private readonly Network _network;
        private readonly snd _sound;
        private readonly CommandFactory _commands;
        private readonly PictureFactory _pictures;

        public LanConfigMenu( IKeyboardInput keyboard, MenuFactory menus, 
			Network network, snd sound, CommandFactory commands, PictureFactory pictures ) : base( "menu_lan_config", keyboard, menus )
		{
			Cursor = -1;
			_JoinName = String.Empty;
			_network = network;
			_sound = sound;
			_pictures = pictures;
			_commands = commands;
        }

		public override void Show( )
		{
			base.Show( );

			if ( Cursor == -1 )
			{
				if ( JoiningGame )
					Cursor = 2;
				else
					Cursor = 1;
			}
			if ( StartingGame && Cursor == 2 )
				Cursor = 1;
			_Port = _network.DefaultHostPort;
			_PortName = _Port.ToString();

			_menus.ReturnOnError = false;
			_menus.ReturnReason = String.Empty;
		}

		public override void KeyEvent( Int32 key )
		{
			switch ( key )
			{
				case KeysDef.K_ESCAPE:
                    _menus.Show( "menu_multiplayer" );
					break;

				case KeysDef.K_UPARROW:
					_sound.LocalSound( "misc/menu1.wav" );
					Cursor--;
					if ( Cursor < 0 )
						Cursor = NUM_LANCONFIG_CMDS;
					break;

				case KeysDef.K_DOWNARROW:
					_sound.LocalSound( "misc/menu1.wav" );
					Cursor++;
					if ( Cursor > NUM_LANCONFIG_CMDS )
						Cursor = 0;
					break;

				case KeysDef.K_ENTER:
					if ( Cursor == 0 )
						break;

					_menus.EnterSound = true;

					M_ConfigureNetSubsystem( );

                    if ( Cursor == 2 )
					{
						if ( StartingGame )
                            _menus.Show( "menu_options" );
						else
                            _menus.Show( "menu_search" );
						break;
					}

					if ( Cursor == 3 )
					{
						_menus.ReturnMenu = this;
						_menus.ReturnOnError = true;
                        _menus.CurrentMenu.Hide();
						_commands.Buffer.Append( String.Format( "connect \"{0}\"\n", _JoinName ) );
						break;
					}
					break;

				case KeysDef.K_BACKSPACE:
					if ( Cursor == 1 )
					{
						if ( !String.IsNullOrEmpty( _PortName ) )
							_PortName = _PortName.Substring( 0, _PortName.Length - 1 );
					}

					if ( Cursor == 3 )
					{
						if ( !String.IsNullOrEmpty( _JoinName ) )
							_JoinName = _JoinName.Substring( 0, _JoinName.Length - 1 );
					}
					break;

				default:
					if ( key < 32 || key > 127 )
						break;

					if ( Cursor == 3 )
					{
						if ( _JoinName.Length < 21 )
							_JoinName += ( Char ) key;
					}

					if ( key < '0' || key > '9' )
						break;

					if ( Cursor == 1 )
					{
						if ( _PortName.Length < 5 )
							_PortName += ( Char ) key;
					}
					break;
			}

			if ( StartingGame && Cursor == 2 )
				if ( key == KeysDef.K_UPARROW )
					Cursor = 1;
				else
					Cursor = 0;

			var k = MathLib.atoi( _PortName );
			if ( k > 65535 )
				k = _Port;
			else
				_Port = k;
			_PortName = _Port.ToString();
		}

        private void DrawPlaque( MenuAdorner adorner )
        {
            var scale = _menus.UIScale;

            //_menus.DrawTransPic( 16 * scale, 4 * scale, _pictures.Cache( "gfx/qplaque.lmp", "GL_NEAREST" ), scale: scale );
            var logoPic = _pictures.Cache( "gfx/qplaque.lmp", "GL_NEAREST" );
            _menus.DrawTransPic( adorner.LeftPoint - ( logoPic.Width * _menus.UIScale ), adorner.MidPointY - ( ( logoPic.Height * scale ) / 2 ), logoPic, scale: scale );

            var p = _pictures.Cache( "gfx/p_multi.lmp", "GL_NEAREST" );
            _menus.DrawPic( adorner.MidPointX - ( ( p.Width * scale ) / 2 ), 4 * scale, p, scale: scale );
        }

        public override void Draw( )
        {
            var scale = _menus.UIScale;
			var lines = 3 + ( JoiningGame ? 2 : 1 ) + ( !String.IsNullOrEmpty( _menus.ReturnReason ) ? 1 : 0 );
            var adorner = _menus.BuildAdorner( lines, 0, 0, width: 152 );

			DrawPlaque( adorner );

            //_menus.DrawTransPic( 16, 4, _pictures.Cache( "gfx/qplaque.lmp", "GL_NEAREST" ) );
			//var p = _pictures.Cache( "gfx/p_multi.lmp", "GL_NEAREST" );
			//var basex = ( 320 - p.Width ) / 2;
			//_menus.DrawPic( basex, 4, p );

			String startJoin;
			if ( StartingGame )
				startJoin = "New Game - TCP/IP";
			else
				startJoin = "Join Game - ^4TCP/IP";

            adorner.Label( startJoin, TextAlignment.Centre );

            adorner.PrintValue( "Address:", _network.MyTcpIpAddress );
            adorner.PrintValue( "Port:", _PortName );

            //_menus.Print( basex, CursorTable[0], "Port" );
			//_menus.DrawTextBox( basex + 8 * 8, CursorTable[0] - 8, 6, 1 );
			//_menus.Print( basex + 9 * 8, CursorTable[0], _PortName );

			if ( JoiningGame )
            {
                adorner.Print( "Search for local games...", TextAlignment.Centre );
                // _menus.Print( basex, CursorTable[1], "Search for local games..." );

                adorner.PrintValue( "Join game at:", _JoinName );
                //_menus.Print( basex, 108, "Join game at:" );
				//_menus.DrawTextBox( basex + 8, CursorTable[2] - 8, 22, 1 );
				//_menus.Print( basex + 16, CursorTable[2], _JoinName );
			}
			else
            {
                adorner.Print( "OK", TextAlignment.Centre );

                //_menus.DrawTextBox( basex, CursorTable[1] - 8, 2, 1 );
				//_menus.Print( basex + 8, CursorTable[1], "OK" );
			}

			var basex = adorner.MidPointX;

			_menus.DrawCharacter( basex - 8, adorner.LineY( Cursor )/*CursorTable[Cursor]*/, 12 + ( ( Int32 ) ( Time.Absolute * 4 ) & 1 ) );

			var isFlash = ( ( Int32 ) ( Time.Absolute * 4 ) & 1 ) == 0;

			if ( Cursor == 1 )
			{
				_menus.DrawCharacter( basex + ( adorner.Padding / 2 ) + _menus.Measure( _PortName ),
					adorner.LineY( Cursor )/*CursorTable[0]*/, isFlash ? ' ' : '|' /* 10 + ( ( Int32 ) ( Time.Absolute * 4 ) & 1 )*/ );
			}

			if ( Cursor == 3 )
			{
				_menus.DrawCharacter( basex + ( adorner.Padding / 2 ) + _menus.Measure( _JoinName ), adorner.LineY( Cursor )/*CursorTable[2]*/,
                    isFlash ? ' ' : '|'/*10 + ( ( Int32 ) ( Time.Absolute * 4 ) & 1 )*/ );
			}

			if ( !String.IsNullOrEmpty( _menus.ReturnReason ) )
                adorner.PrintWhite( "_menus.ReturnReason", TextAlignment.Centre );
            //	_menus.PrintWhite( basex, 148, _menus.ReturnReason );
        }


        private void M_ConfigureNetSubsystem( )
        {
			_commands.Buffer.Append( "stopdemo\n" );
            _network.HostPort = _Port;
        }
    }
}
