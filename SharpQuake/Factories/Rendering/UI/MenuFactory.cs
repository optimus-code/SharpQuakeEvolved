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
/// 

using SharpQuake.Desktop;
using SharpQuake.Framework;
using SharpQuake.Framework.Factories;
using SharpQuake.Framework.Factories.IO;
using SharpQuake.Framework.IO;
using SharpQuake.Framework.IO.Input;
using SharpQuake.Networking.Client;
using SharpQuake.Renderer.Textures;
using SharpQuake.Rendering;
using SharpQuake.Rendering.UI;
using SharpQuake.Rendering.UI.Menus;
using SharpQuake.Sys;
using System;
using System.Drawing;

// menu.h
// menu.c

namespace SharpQuake.Factories.Rendering.UI
{
	/// <summary>
	/// M_functions
	/// </summary>
	public class MenuFactory : BaseFactory<String, BaseMenu>
	{
		public Int32 UIScale
		{
			get
			{
				return 4;
			}
		}

		public BaseMenu CurrentMenu
		{
			get;
			private set;
		}

		public Action OnToggleConsole
		{
			get;
			set;
		}

		public Func<Boolean> OnDrawConsole
		{
			get;
			set;
		}

		public Boolean EnterSound;
		public Boolean ReturnOnError;
		public String ReturnReason;
		public BaseMenu ReturnMenu;
		private const Int32 SLIDER_RANGE = 10;

		//qboolean	m_entersound	// play after drawing a frame, so caching

		// won't disrupt the sound
		private Boolean _RecursiveDraw; // qboolean m_recursiveDraw

		private Byte[] _IdentityTable = new Byte[256]; // identityTable
		private Byte[] _TranslationTable = new Byte[256]; //translationTable


        /// <summary>
        /// Statically defined list of menus
        /// </summary>
        /// <remarks>
        /// (Is added to DI hence the static list.)
        /// </remarks>
        public static Type[] FACTORY_TYPES = new Type[]
        {
			// Top Level
			typeof( MainMenu ),
            typeof( SinglePlayerMenu ),
            typeof( LoadMenu ),
            typeof( SaveMenu ),
            typeof( MultiplayerMenu ),
            typeof( OptionsMenu ),
            typeof( VideoMenu ),
            typeof( HelpMenu ),
            typeof( QuitMenu ),
			
			// Submenus
            typeof( KeysMenu ),
            typeof( LanConfigMenu ),
            typeof( SetupMenu ),
            typeof( GameOptionsMenu ),
            typeof( SearchMenu ),
            typeof( ServerListMenu ),
        };

        private readonly IEngine _engine;
        private readonly IKeyboardInput _keyboard;
		private readonly CommandFactory _commands;
		private readonly PictureFactory _pictures;
		private readonly Scr _screen;
		private readonly Vid _video;
		private readonly Drawer _drawer;
		private readonly snd _sound;
        private readonly VideoState _videoState;
		private readonly ClientState _clientState;

        public MenuFactory( IEngine engine, IKeyboardInput keyboard, CommandFactory commands, 
			PictureFactory pictures, Scr screen, Vid video, Drawer drawer, snd sound,
			VideoState videoState, ClientState clientState )
		{
			_engine = engine;
			_keyboard = keyboard;
			_commands = commands;
			_pictures = pictures;
			_screen = screen;
			_video = video;
			_drawer = drawer;
			_sound = sound;
			_videoState = videoState;
			_clientState = clientState;

			_keyboard.Listen( KeyDestination.key_menu, KeyDown );
            _keyboard.Listen( KeyDestination.key_game, ToggleConsoleCheck );
            _keyboard.Listen( KeyDestination.key_console, ToggleConsoleCheck );


            ///_keyboard.OnMenuKeyDown += KeyDown;
			//_keyboard.OnToggleConsole += ( ) => ToggleMenu_f( null );
			_screen.OnDrawMenus += Draw;
        }

        /// <summary>
        /// Initialise all menus
        /// </summary>
        /// <remarks>
        /// (This also makes sure the DI instantiates the factory items,
        /// without this; they would not get instantiated unless directly
        /// referenced.)
        /// </remarks>
        private void InitialiseMenus( )
		{
			foreach ( var menuType in FACTORY_TYPES )
			{
				var instance = ( BaseMenu ) _engine.Get( menuType );

                // Add them to the factory to save having to grab instances from DI
                Add( instance.Name, instance );
			}
		}

		/// <summary>
		/// M_Init
		/// </summary>
		public void Initialise( )
		{
			_commands.Add( "togglemenu", ToggleMenu_f );
            InitialiseMenus();
		}

		/// <summary>
		/// Add a menu to the factory
		/// </summary>
		/// <remarks>
		/// (Overridden to automatically add the command; routing to our
		/// generic menu method which interprets the correct menu.)
		/// </remarks>
		/// <param name="key"></param>
		/// <param name="item"></param>
		public override void Add( String key, BaseMenu item )
		{
			base.Add( key, item );

			// Automatically setup command
			_commands.Add( key, Generic_Menu_f );
		}

		/// <summary>
		/// Show a menu
		/// </summary>
		/// <param name="name"></param>
		public void Show( String name )
		{
			var menu = Get( name );

			if ( menu != null )
				menu.Show( );
		}

		/// <summary>
		/// Set the active menu
		/// </summary>
		/// <param name="menu"></param>
		public void SetActive( BaseMenu menu )
		{
			CurrentMenu = menu;
			_video.Device.Desc.BlurPostFX = menu != null;
		}

		/// <summary>
		/// Show a menu
		/// </summary>
		/// <remarks>
		///	(Routed through the command system.)
		/// </remarks>
		/// <param name="msg"></param>
		private void Generic_Menu_f( CommandMessage msg )
		{
			var menuName = msg.Name;
			Show( menuName );
		}

		/// <summary>
		/// M_Keydown
		/// </summary>
		public void KeyDown( Int32 key )
		{
			CurrentMenu?.KeyEvent( key );
		}

		/// <summary>
		/// M_Draw
		/// </summary>
		public void Draw( )
		{
			if ( CurrentMenu == null || _keyboard.Destination != KeyDestination.key_menu )
				return;

			if ( !_RecursiveDraw )
			{
                _videoState.ScreenCopyEverything = true;

				if ( OnDrawConsole?.Invoke() == true )
				{
					_sound.ExtraUpdate();
				}
				else
					_drawer.FadeScreen();

				_screen.FullUpdate = 0;
			}
			else
			{
				_RecursiveDraw = false;
			}

			CurrentMenu?.Draw();

			if ( EnterSound )
			{
				_sound.LocalSound( "misc/menu2.wav" );
				EnterSound = false;
			}

			_sound.ExtraUpdate();
		}

		/// <summary>
		/// M_ToggleMenu_f
		/// </summary>
		public void ToggleMenu_f( CommandMessage msg )
		{
			EnterSound = true;

			if ( _keyboard.Destination == KeyDestination.key_menu )
			{
				if ( CurrentMenu.Name != "menu_menu" )
				{
					Show( "menu_main" );
					return;
				}
				CurrentMenu.Hide();
				return;
			}

			if ( _keyboard.Destination == KeyDestination.key_console )
				_commands.Buffer.Append( "toggleconsole\n" );
			else
				Show( "menu_main" );
		}

        public void DrawPic( Int32 x, Int32 y, BasePicture pic, int scale = 1 )
		{
			_video.Device.Graphics.DrawPicture( pic, x /*+ ( ( _videoState.Data.width - ( 320 * scale ) ) >> 1 )*/, y, scale: scale );
		}

		public void DrawTransPic( Int32 x, Int32 y, BasePicture pic, int scale = 1 )
		{
			_video.Device.Graphics.DrawPicture( pic, x /*+ ( ( _videoState.Data.width - ( 320 * scale ) ) >> 1 )*/, y, hasAlpha: true, scale: scale );
		}

		/// <summary>
		/// M_DrawTransPicTranslate
		/// </summary>
		public void DrawTransPicTranslate( Int32 x, Int32 y, BasePicture pic )
		{
			_drawer.TransPicTranslate( x /*+ ( ( _videoState.Data.width - 320 ) >> 1 )*/, y, pic, _TranslationTable );
		}

		/// <summary>
		/// M_Print
		/// </summary>
		public void Print( Int32 cx, Int32 cy, String str, Int32 scale = 1, Boolean forceCharset = false, Boolean isBigFont = false )
        {
            Color? colour = null;
			var ttf = Cvars.TrueTypeFonts.Get<Boolean>( );

            if ( ttf )
                colour = Colours.Quake;

			var defaultColour = colour; 

            for ( var i = 0; i < str.Length; i++ )
			{
				var c = str[i];

				if ( !ttf ) // Force colour change for vanilla font
					c = ( Char ) ( c + 128 );

				int color = -1;
				var inColorCode = str[i] == '^' && i + 1 < str.Length && Int32.TryParse( "" + str[i+1], out color );

				if ( inColorCode && color >= 0 )
				{
                    colour = Colours.FromCode( color, defaultColour );

                    i += 1;
					continue;
				}

                DrawCharacter( cx, cy, c, scale, forceCharset, colour , isBigFont );
				cx += _drawer.CharacterAdvance( forceCharset, isBigFont ) + _drawer.MeasureCharacter( ( Char ) c, forceCharset, isBigFont );
			}
		}

		/// <summary>
		/// M_DrawCharacter
		/// </summary>
		public void DrawCharacter( Int32 cx, Int32 line, Int32 num, Int32 scale = 1, Boolean forceCharset = false, Color? color = null, Boolean isBigFont = false )
		{
			_drawer.DrawCharacter( cx /*+ ( ( _videoState.Data.width - ( 320 * scale ) ) >> 1 )*/, line, num, forceCharset, color, isBigFont );
        }

        public void DrawCharacterStretched( Int32 cx, Int32 line, Int32 num, Int32 width )
        {
            _drawer.DrawCharacterStretched( cx /*+ ( ( _videoState.Data.width - ( 320 * scale ) ) >> 1 )*/, line, num, width );
        }

        /// <summary>
        /// M_PrintWhite
        /// </summary>
        public void PrintWhite( Int32 cx, Int32 cy, String str, Int32 scale = 1, Boolean forceCharset = false, Boolean isBigFont = false )
		{
			Color? colour = null;

			if ( Cvars.TrueTypeFonts.Get<Boolean>( ) )
                colour = Color.White;

            var defaultColour = colour;

            for ( var i = 0; i < str.Length; i++ )
			{
				var c = str[i];
                int color = -1;
                var inColorCode = str[i] == '^' && i + 1 < str.Length && int.TryParse( "" + str[i + 1], out color );

                if ( inColorCode && color >= 0 )
                {
                    switch ( color )
                    {
                        case 0:
                            colour = defaultColour;
                            break;

                        case 1:
                            colour = Color.Black;
                            break;

                        case 2:
                            colour = Color.Red;
                            break;

                        case 3:
                            colour = Color.Green;
                            break;

                        case 4:
                            colour = Color.Yellow;
                            break;

                        case 5:
                            colour = Color.Blue;
                            break;

                        case 6:
                            colour = Color.Cyan;
                            break;

                        case 7:
                            colour = Color.Pink;
                            break;

                        case 8:
                            colour = Color.White;
                            break;

                        case 9:
                            colour = Color.Gray;
                            break;
                    }
                    i += 1;
                    continue;
                }

                DrawCharacter( cx, cy, c, scale, forceCharset, colour, isBigFont );
                cx += _drawer.CharacterAdvance( forceCharset, isBigFont ) + _drawer.MeasureCharacter( ( Char ) c, forceCharset, isBigFont );
            }
		}

		
        public void DrawTextBoxCalc( Int32 x, Int32 y, Int32 width, Int32 height, Int32 scale = 1 )
		{
			//var w = ( 8 ) * ( width / ( 8 * scale ) );

            DrawTextBox( x, y, width, height / _drawer.CharacterAdvanceHeight( ), scale );

        }

        /// <summary>
        /// M_DrawTextBox
        /// </summary>
        public void DrawTextBox( Int32 x, Int32 y, Int32 width, Int32 lines, Int32 scale = 1 )
		{
			// draw left side
			var w = width;
			var cx = x;
			var cy = y;
			var p = _pictures.Cache( "gfx/box_tl.lmp", "GL_NEAREST" );
			DrawTransPic( cx, cy, p, scale: scale );
			p = _pictures.Cache( "gfx/box_ml.lmp", "GL_NEAREST" );
			for ( var n = 0; n < lines; n++ )
			{
				cy += ( 8 * scale );
				DrawTransPic( cx, cy, p, scale: scale );
			}
			p = _pictures.Cache( "gfx/box_bl.lmp", "GL_NEAREST" );
			DrawTransPic( cx, cy + ( 8 * scale ), p, scale: scale );

			// draw middle
			cx += ( 8 * scale );

			while ( w > 0 )
			{
				cy = y;
				p = _pictures.Cache( "gfx/box_tm.lmp", "GL_NEAREST" );
				DrawTransPic( cx, cy, p, scale: scale );
				p = _pictures.Cache( "gfx/box_mm.lmp", "GL_NEAREST" );
				for ( var n = 0; n < lines; n++ )
				{
					cy += ( 8 * scale );
					if ( n == 1 )
						p = _pictures.Cache( "gfx/box_mm2.lmp", "GL_NEAREST" );
					DrawTransPic( cx, cy, p, scale: scale );
				}
				p = _pictures.Cache( "gfx/box_bm.lmp", "GL_NEAREST" );
				DrawTransPic( cx, cy + ( 8 * scale ), p, scale: scale );
				w -= ( 2 * scale );
				cx += ( 16 * scale );
			}

			// draw right side
			cy = y;
			p = _pictures.Cache( "gfx/box_tr.lmp", "GL_NEAREST" );
			DrawTransPic( cx, cy, p, scale: scale );
			p = _pictures.Cache( "gfx/box_mr.lmp", "GL_NEAREST" );
			for ( var n = 0; n < lines; n++ )
			{
				cy += ( 8 * scale );
				DrawTransPic( cx, cy, p, scale: scale );
			}
			p = _pictures.Cache( "gfx/box_br.lmp", "GL_NEAREST" );
			DrawTransPic( cx, cy + ( 8 * scale), p, scale: scale );
		}

		/// <summary>
		/// M_DrawSlider
		/// </summary>
		public void DrawSlider( Int32 x, Int32 y, Single range, Int32 scale = 1 )
		{
			if ( range < 0 )
				range = 0;

			if ( range > 1 )
				range = 1;

			DrawCharacter( x - ( 8 * scale ), y, 128, scale: scale, forceCharset: true );
			Int32 i;

			for ( i = 0; i < SLIDER_RANGE; i++ )
				DrawCharacter( x + ( ( i * 8 ) * scale ), y, 129, scale: scale, forceCharset: true );

			DrawCharacter( x + ( ( i * 8 ) * scale ), y, 130, scale: scale, forceCharset: true );

			DrawCharacter( ( Int32 ) ( x + ( ( SLIDER_RANGE - 1 ) * ( 8 * scale ) * range ) ), y, 131, scale: scale, forceCharset: true );
		}

        public void DrawSlider( Int32 x, Int32 y, Int32 width, Single range, Int32 scale = 1 )
        {
            if ( range < 0 )
                range = 0;

            if ( range > 1 )
                range = 1;

            DrawCharacter( x - ( 8 * scale ), y, 128, scale: scale, forceCharset: true );
            Int32 i = 0;

			var sliderRange = ( int ) Math.Ceiling( width / ( 8d * scale ) ) + 3;
            DrawCharacterStretched( x + ( ( 0 * 8 ) * scale ), y, 129, ( width  * 2 ) - ( 24 * scale ) );
			//for ( i = 0; i < sliderRange; i++ )
			//    DrawCharacter( x + ( ( i * 8 ) * scale ), y, 129, scale: scale, forceCharset: true );

			i += sliderRange;
            //DrawCharacter( x + ( ( i * 8 ) * scale ), y, 130, scale: scale, forceCharset: true );
            DrawCharacter( x + ( width * 2 ) - ( 24 * scale ), y, 130, scale: scale, forceCharset: true );

			var extent = ( x + ( width * 2 ) - ( 24 * scale ) ) - ( x - ( 8 * scale ) );
			// Cursor

			var halfC = ( 8 * scale ) / 2;
            //DrawCharacter( ( Int32 ) ( x + ( ( sliderRange - 1 ) * ( 8 * scale ) * range ) ), y, 131, scale: scale, forceCharset: true );
            DrawCharacter( ( Int32 ) ( x - halfC + ( ( extent - ( halfC * 1.5 ) ) * range ) ), y, 131, scale: scale, forceCharset: true );
        }

        /// <summary>
        /// M_DrawCheckbox
        /// </summary>
        public void DrawCheckbox( Int32 x, Int32 y, Boolean on, Int32 scale = 1 )
		{
			if ( on )
                PrintWhite( x, y, "ON", scale: scale );
			else
				PrintWhite( x, y, "OFF", scale: scale );
		}

		/// <summary>
		/// M_BuildTranslationTable
		/// </summary>
		public void BuildTranslationTable( Int32 top, Int32 bottom )
		{
			for ( var j = 0; j < 256; j++ )
				_IdentityTable[j] = ( Byte ) j;

			_IdentityTable.CopyTo( _TranslationTable, 0 );

			if ( top < 128 )    // the artists made some backwards ranges.  sigh.
				Array.Copy( _IdentityTable, top, _TranslationTable, render.TOP_RANGE, 16 ); // memcpy (dest + Render.TOP_RANGE, source + top, 16);
			else
				for ( var j = 0; j < 16; j++ )
					_TranslationTable[render.TOP_RANGE + j] = _IdentityTable[top + 15 - j];

			if ( bottom < 128 )
				Array.Copy( _IdentityTable, bottom, _TranslationTable, render.BOTTOM_RANGE, 16 ); // memcpy(dest + Render.BOTTOM_RANGE, source + bottom, 16);
			else
				for ( var j = 0; j < 16; j++ )
					_TranslationTable[render.BOTTOM_RANGE + j] = _IdentityTable[bottom + 15 - j];
		}

        public Int32 Measure( String text, Boolean forceCharset = false )
        {
            return _drawer.MeasureString( text, forceCharset );
        }

        public Int32 Measure( Char character, Boolean forceCharset = false )
        {
            return _drawer.MeasureCharacter( character, forceCharset );
        }

        public Int32 CharacterAdvanceHeight(Boolean forceCharset = false )
        {
            return _drawer.CharacterAdvanceHeight( forceCharset );
        }

        public MenuAdorner BuildAdorner( Int32 totalLines, Int32 x, Int32 y, Int32 padding = 4, Int32? width = null, Boolean isBigFont = false )
		{
			return new MenuAdorner( this, _drawer, _videoState, totalLines, x, y, width.HasValue ? width.Value : Cvars.TrueTypeFonts.Get<Boolean>() ? 108 : 152, padding * UIScale, isBigFont );
        }

		private void ShowConsoleOrMenu()
		{
            ToggleMenu_f( null );
        }

		private void ToggleConsoleCheck( Int32 key )
		{
			//
			// during demo playback, most keys bring up the main menu
			//
			var isConsoleValidChar = _keyboard.IsValidConsoleCharacter( ( Char ) key );
			var down = _keyboard.IsKeyDown( key );

            if ( _keyboard.IsWatchingDemo && down && isConsoleValidChar && _keyboard.Destination == KeyDestination.key_game )
            {
				ShowConsoleOrMenu( );
                return;
            }

            if ( key == KeysDef.K_ESCAPE )
                ShowConsoleOrMenu( );
        }
	}
}
