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
using SharpQuake.Framework.IO;
using SharpQuake.Framework.IO.Input;
using SharpQuake.Framework.Logging;
using SharpQuake.Framework.Rendering.UI;
using SharpQuake.Game.Client;
using SharpQuake.Logging;
using SharpQuake.Networking.Client;
using SharpQuake.Renderer.Textures;
using SharpQuake.Sys;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace SharpQuake.Rendering.UI.Elements
{
    /// <summary>
    /// Visual side of the console system
    /// </summary>
    public class VisualConsole : BaseUIElement, IResetableRenderer, IGameConsoleLogger
	{
		const Char PREFIX = ']';

        public override Boolean ManualInitialisation
        {
            get
            {
                return true;
            }
        }

        // qboolean con_forcedup		// because no entities to refresh
        public Boolean IsForcedUp
		{
			get;
			private set;
		}

		// con_notifylines	// scan lines to clear for notify lines
		private Int32 NotifyLines
		{
			get;
			set;
		}

		// con_totallines   // total lines in console scrollback
		private Int32 TotalLines
		{
			get;
			set;
		}

		private Int32 BackScroll
		{
			get;
			set;
		}

		private const String LOG_FILE_NAME = "qconsole.log";

		private const Int32 CON_TEXTSIZE = 16384;
		private const Int32 NUM_CON_TIMES = 4;

		private Char[] Text
		{
			get;
			set;
		} = new Char[CON_TEXTSIZE]; // char		*con_text=0;

		private Int32 _VisLines; // con_vislines

		// con_backscroll		// lines up from bottom to display
		private Int32 Current
		{
			get;
			set;
		} // con_current		// where next message will be printed

		private Int32 X
		{
			get;
			set;
		} // con_x		// offset in current line for next print

		private Int32 CR
		{
			get;
			set;
		} // from Print()

		private Double[] Times
		{
			get;
			set;
		} = new Double[NUM_CON_TIMES]; // con_times	// realtime time the line was generated

		// for transparent notify lines
		private Int32 LineWidth // con_linewidth
		{
			get;
			set;
		}

		private Single CursorSpeed
		{
			get;
			set;
		} = 4; // con_cursorspeed

		public Boolean IsInitialised
		{
			get;
			private set;
		}

		private BasePicture ConsoleBackground
		{
			get;
			set;
		}

		private TextPicture Background
		{
			get;
			set;
		}

		//scr_con_current
		public Single ConCurrent
        {
            get;
            private set;
        }

        private Single _ConLines;		// lines of console to display
        private Int32 _ClearConsole; // clearconsole
                                     // clearnotify

        private StringBuilder LinesBuffer
        {
            get;
            set;
        } = new StringBuilder( CON_TEXTSIZE );

        private List<String> History
		{
			get;
			set;
		} = new List<String>( );

		private Int32 HistoryPosition
		{
			get;
			set;
		} = -1;

		private StringBuilder InputBuffer
		{
			get;
			set;
		} = new StringBuilder( 256 );

		private Int32 CursorPosition
		{
			get;
			set;
		}

        private Queue<(Double Time, String Text)> NotificationWall
		{
			get;
			set;
		} = new Queue<(Double, String)>( NUM_CON_TIMES );

        private StringBuilder NotificationBuffer
		{
			get;
			set;
		} = new StringBuilder( );

		private List<String> Autocomplete
		{
			get;
			set;
		} = new List<String>( 10 );

        private readonly IEngine _engine;
		private readonly IConsoleLogger _logger;
		private readonly IKeyboardInput _keyboard;
		private readonly CommandFactory _commands;
		private readonly ClientVariableFactory _cvars;
		private readonly MenuFactory _menus;
		private readonly ClientState _clientState;
		private readonly Scr _screen;
		private readonly Vid _video;
		private readonly Drawer _drawer;
		private readonly snd _sound;
		private readonly View _view;
		private readonly VideoState _videoState;

		public VisualConsole( IEngine engine, IConsoleLogger logger, IKeyboardInput keyboard,
            ClientState clientState, Scr screen, Vid video, Drawer drawer, View view, snd sound,
            CommandFactory commands, ClientVariableFactory cvars, MenuFactory menus,
			VideoState videoState )
		{
			_engine = engine;
			_logger = logger;
			_keyboard = keyboard;
			_clientState = clientState;
			_screen = screen;
			_view = view;
			_commands = commands;
			_cvars = cvars;

			_menus = menus;
			_menus.OnToggleConsole += ( ) =>
			{ 
				ResetNotificationWall( );
                ResetInput( );
            };

			_menus.OnDrawConsole += ( ) =>
			{
				if ( ConCurrent > 0 )
				{
					DrawConsoleBackground( _videoState.Data.height );
					return true;
				}

				return false;
			};

            _sound = sound;
			_video = video;
			_drawer = drawer;
			_videoState = videoState;
			_logger.OnPrint += OnPrint;
			//_keyboard.OnConsoleScroll += KeyConsoleScroll;
			//_keyboard.OnConsoleSubmit += KeyConsoleEnter;

			// Replicate previous switch for keyboard input listeners
			_keyboard.Listen( KeyDestination.key_console, KeyConsole );
            _keyboard.Listen( KeyDestination.key_game, KeyConsole );

			ResetInput( );

        }

        public override void Initialise()
        {
            _commands.Add( "toggleconsole", ToggleConsole_f );

            LineWidth = -1;
			CheckResize( );

			Print( "Console initialized.\n" );

			//
			// register our commands
			//
			if ( Cvars.NotifyTime == null )
				Cvars.NotifyTime = _cvars.Add( "con_notifytime", 3 );

			_commands.Add( "messagemode", MessageMode_f );
			_commands.Add( "messagemode2", MessageMode2_f );
			_commands.Add( "clear", Clear_f );

			HasInitialised = true;
        }

        /// <summary>
        /// SCR_SetUpToDrawConsole
        /// </summary>
        public void Configure()
        {
            CheckResize( );

            if ( _screen.Elements.IsVisible( ElementFactory.LOADING ) )
                return;     // never a console with loading plaque

            // decide on the height of the console
            IsForcedUp = ( _clientState.Data.worldmodel == null ) || ( _clientState.StaticData.signon != ClientDef.SIGNONS );
			_view.NoRender = IsForcedUp; // Pass the status to the view - this is to break circular dependency with view <-> console.

			if ( IsForcedUp )
            {
                _ConLines = _videoState.Data.height; // full screen
                ConCurrent = _ConLines;
            }
            else if ( _keyboard.Destination == KeyDestination.key_console )
                _ConLines = _videoState.Data.height / 2; // half screen
            else
                _ConLines = 0; // none visible

            if ( _ConLines < ConCurrent )
            {
                ConCurrent -= ( Int32 ) ( Cvars.ConSpeed.Get<Int32>( ) * Time.Delta );
                if ( _ConLines > ConCurrent )
                    ConCurrent = _ConLines;
            }
            else if ( _ConLines > ConCurrent )
            {
                ConCurrent += ( Int32 ) ( Cvars.ConSpeed.Get<Int32>( ) * Time.Delta );
                if ( _ConLines < ConCurrent )
                    ConCurrent = _ConLines;
            }

            if ( _ClearConsole++ < _videoState.Data.numpages )
            {
                _screen.Elements.SetDirty( ElementFactory.HUD );
            }
            else if ( _screen.ClearNotify++ < _videoState.Data.numpages )
            {
                //????????????
            }
           // else
           //     NotifyLines = 0;
        }

        /// <summary>
        /// SCR_DrawConsole
        /// </summary>
        public override void Draw( )
        {
            base.Draw( );

            if ( !HasInitialised || !IsInitialised )
                return;

            if ( ConCurrent > 0 )
            {
                _videoState.ScreenCopyEverything = true;
                Draw( ( Int32 ) ConCurrent, true );
                DrawAutocomplete( );
                _ClearConsole = 0;
                NotifyLines = 0;
            }
            else if ( _keyboard.Destination == KeyDestination.key_game ||
                _keyboard.Destination == KeyDestination.key_message )
            {
                DrawNotify( );  // only draw notify in game
                NotifyLines = 4;
            }
        }

        public void Reset( )
        {
            ConCurrent = 0;
			LinesBuffer.Clear( );
            ResetInput( );
			ResetNotificationWall( );
        }

		/// <summary>
		/// Executed by the core console logging functions to propagate messages
		/// to the visual/game elements.
		/// </summary>
		/// <param name="message"></param>
		private void OnPrint( String message )
		{
			if ( !IsInitialised )
				return;

			if ( _engine.IsDedicated )
				return;     // no graphics mode

			// write it to the scrollable buffer
			Print( message );

			// update the screen if the console is displayed
			if ( _clientState.StaticData.signon != ClientDef.SIGNONS && !_videoState.IsScreenDisabledForLoading )
				_screen.UpdateScreen( );
		}

		// Con_Print (char *txt)
		//
		// Handles cursor positioning, line wrapping, etc
		// All console printing must go through this in order to be logged to disk
		// If no console is visible, the notify window will pop up.
		private void Print( String txt )
		{
			if ( String.IsNullOrEmpty( txt ) )
				return;

			LinesBuffer.Append( txt );

			foreach ( var character in txt )
			{
				if ( character == '\n' )
                {
                    NotificationWall.Enqueue( (Time.Absolute, NotificationBuffer.ToString()) );
                    NotificationBuffer.Clear( );
                }
				else
					NotificationBuffer.Append( character );
			}

            return;


			Int32 mask, offset = 0;

			BackScroll = 0;

			if ( txt.StartsWith( ( ( Char ) 1 ).ToString( ) ) )// [0] == 1)
			{
				mask = 128; // go to colored text
				_sound.LocalSound( "misc/talk.wav" ); // play talk wav
				offset++;
			}
			else if ( txt.StartsWith( ( ( Char ) 2 ).ToString( ) ) ) //txt[0] == 2)
			{
				mask = 128; // go to colored text
				offset++;
			}
			else
				mask = 0;

			while ( offset < txt.Length )
			{
				var c = txt[offset];

				Int32 l;
				// count word length
				for ( l = 0; l < LineWidth && offset + l < txt.Length; l++ )
				{
					if ( txt[offset + l] <= ' ' )
						break;
				}

				// word wrap
				if ( l != LineWidth && ( X + l > LineWidth ) )
					X = 0;

				offset++;

				if ( CR != 0 )
				{
					Current--;
					CR = 0;
				}

				if ( X == 0 )
				{
					LineFeed( );
					// mark time for transparent overlay
					if ( Current >= 0 )
						Times[Current % NUM_CON_TIMES] = Time.Absolute; // realtime
				}

				switch ( c )
				{
					case '\n':
						X = 0;
						break;

					case '\r':
						X = 0;
						CR = 1;
						break;

					default:    // display character and advance
						var y = Current % TotalLines;
						Text[y * LineWidth + X] = ( Char ) ( c | mask );
						X++;
						if ( X >= LineWidth )
							X = 0;
						break;
				}
			}
		}

		// Con_CheckResize (void)
		private void CheckResize( )
		{
			var width = ( _videoState.Data.width >> 3 ) - 2;
			if ( width == LineWidth )
				return;

			if ( width < 1 ) // video hasn't been initialized yet
			{
				width = 38;
				LineWidth = width; // con_linewidth = width;
				TotalLines = CON_TEXTSIZE / LineWidth;
				Utilities.FillArray( Text, ' ' ); // Q_memset (con_text, ' ', CON_TEXTSIZE);
			}
			else
			{
				var oldwidth = LineWidth;
				LineWidth = width;
				var oldtotallines = TotalLines;
				TotalLines = CON_TEXTSIZE / LineWidth;
				var numlines = oldtotallines;

				if ( TotalLines < numlines )
					numlines = TotalLines;

				var numchars = oldwidth;

				if ( LineWidth < numchars )
					numchars = LineWidth;

				var tmp = Text;
				Text = new Char[CON_TEXTSIZE];
				Utilities.FillArray( Text, ' ' );

				for ( var i = 0; i < numlines; i++ )
				{
					for ( var j = 0; j < numchars; j++ )
					{
						Text[( TotalLines - 1 - i ) * LineWidth + j] = tmp[( ( Current - i + oldtotallines ) %
									  oldtotallines ) * oldwidth + j];
					}
				}

				ClearNotify( );
			}

			BackScroll = 0;
			Current = TotalLines - 1;
		}

		/// <summary>
		/// Processes notification messages
		/// </summary>
		private void NotificationsTick()
        {
			if ( NotificationWall.Count == 0 )
				return;

            var line = NotificationWall.Peek( );

			if ( line.Time == 0 )
			{
				NotificationWall.Dequeue( );
				return;
			}

            var time = Time.Absolute - line.Time;

			if ( time > Cvars.NotifyTime.Get<Int32>( ) )
            {
                NotificationWall.Dequeue( );
                return;
			}
        }

		public void DrawUIBox( int x, int y, int width, int height )
		{
            _video.Device.Graphics.Fill( x, y, width, height, HudResources.NEWUI_BG );
            _video.Device.Graphics.LineFill( x, y, width, height, HudResources.NEWUI_BORDER, 1 );
        }

		/// <summary>
		/// Draw the auto-complete window that pops up when typing
		/// </summary>
		private void DrawAutocomplete()
		{
			if ( Autocomplete.Count == 0 )
				return;

			var inputBuffer = InputBuffer.ToString( ).Substring( 1 ).Trim( );

            var doubleHeight = _drawer.CharacterAdvance( ) * 2;
            var linesWithPadding = ( _VisLines - doubleHeight );

            var rows = ( Int32 ) Math.Ceiling( linesWithPadding / ( Double ) _drawer.CharacterAdvanceHeight( ) ) - 1;
            var y = _drawer.CharacterAdvance( ) + ( _drawer.CharacterAdvanceHeight( ) * rows );

			var padding = 16;
            var yAdvance = y + 4 + padding;
            var xAdvance = padding;

			var displayCount = Math.Min( Autocomplete.Count, 6 );
			var longestWidth = Autocomplete.Max( a => _drawer.MeasureString( a ) );
			var showEllipsis = ( displayCount < Autocomplete.Count );
            var height = _drawer.CharacterAdvanceHeight( ) * ( displayCount + ( showEllipsis? 1 : 0 ) ) + ( padding * 2 );
			var newUI = Cvars.NewUI.Get<Boolean>( );

			if ( newUI )
			{
				DrawUIBox( 0, yAdvance - padding, longestWidth + ( padding * 2 ), height );
			}
			else
			{
				_drawer.DrawFrame( new Rectangle( 0, yAdvance - padding, longestWidth + ( padding * 2 ), height ), 4 );
			}

			foreach ( var line in Autocomplete.Take( displayCount ) )
            {
				var text = line;

				if ( text.StartsWith( inputBuffer ) )
					text = $"^8{inputBuffer}^0" + text.Substring( inputBuffer.Length );

                _drawer.DrawRichString( xAdvance, yAdvance, text, colour: Colours.Grey );

                yAdvance += _drawer.CharacterAdvanceHeight( );
            }

			if ( showEllipsis )
                _drawer.DrawRichString( xAdvance, yAdvance, ( Autocomplete.Count - displayCount ) + " more...", colour: Colours.Grey );
		}

		/// <summary>
		/// Con_DrawNotify
		/// </summary>
		private void DrawNotify( )
		{
			NotificationsTick( );

            var v = 0;// _drawer.CharacterAdvanceHeight() - _drawer.CharacterAdvance();

			if ( NotifyLines > 0 && NotificationWall.Count > 0 )
			{
				var yAdvance = 0;
                var xAdvance = _drawer.CharacterAdvance( );

                foreach ( var line in NotificationWall.Take( NotifyLines ) )
				{
                    _drawer.DrawRichString( xAdvance, yAdvance, line.Text, colour: Color.White );

                    yAdvance += _drawer.CharacterAdvanceHeight( );
				}
			}

			return;

			for ( var i = Current - NUM_CON_TIMES + 1; i <= Current; i++ )
			{
				if ( i < 0 )
					continue;

				var time = Times[i % NUM_CON_TIMES];

				if ( time == 0 )
					continue;

				time = Time.Absolute - time;

				if ( time > Cvars.NotifyTime.Get<Int32>( ) )
					continue;

				var textOffset = ( i % TotalLines ) * LineWidth;

				_screen.ClearNotify = 0;
				_videoState.ScreenCopyTop = true;

				var xAdvance = _drawer.CharacterAdvance();

				for ( var x = 0; x < LineWidth; x++ )
				{
					var c = Text[textOffset + x];
                    _drawer.DrawCharacter( xAdvance, v, c );
					xAdvance += _drawer.CharacterAdvance( ) + _drawer.MeasureCharacter( c );
                }

				v += _drawer.CharacterAdvanceHeight();
			}

			return;

			if ( _keyboard.Destination == KeyDestination.key_message )
			{
				_screen.ClearNotify = 0;
                _videoState.ScreenCopyTop = true;

				var x = 0;
				var xAdvance = _drawer.CharacterAdvance( );
				var sayLength = _drawer.MeasureString( "say: " );

                _drawer.DrawString( xAdvance, v, "say:" );

				xAdvance += sayLength;

                var chat = _keyboard.ChatBuffer;
				for ( ; x < chat.Length; x++ )
				{
					_drawer.DrawCharacter( xAdvance, v, chat[x] );
					xAdvance += _drawer.CharacterAdvance( ) + _drawer.MeasureCharacter( chat[x] );
                }
				_drawer.DrawCharacter( xAdvance, v, 10 + ( ( Int32 ) ( Time.Absolute * CursorSpeed ) & 1 ) );
				v += _drawer.CharacterAdvanceHeight( );
			}

			if ( v > NotifyLines )
				NotifyLines = v;
		}

		/// <summary>
		/// Con_ClearNotify
		/// </summary>
		public void ClearNotify( )
		{
			NotificationWall.Clear( );

			//for ( var i = 0; i < NUM_CON_TIMES; i++ )
			//	Times[i] = 0;
		}

		/// <summary>
		/// Con_Clear_f
		/// </summary>
		private void Clear_f( CommandMessage msg )
		{
			LinesBuffer.Clear( );
            //Utilities.FillArray( Text, ' ' );
		}

		/// <summary>
		/// Con_MessageMode_f
		/// </summary>
		/// <param name="msg"></param>
		private void MessageMode_f( CommandMessage msg )
		{
			_keyboard.Destination = KeyDestination.key_message;
			_keyboard.TeamMessage = false;
		}

		/// <summary>
		/// Con_MessageMode2_f
		/// </summary>
		/// <param name="msg"></param>
		private void MessageMode2_f( CommandMessage msg )
		{
			_keyboard.Destination = KeyDestination.key_message;
			_keyboard.TeamMessage = true;
		}

		/// <summary>
		/// Con_Linefeed
		/// </summary>
		private void LineFeed( )
		{
			LinesBuffer.AppendLine( );
		}

		/// <summary>
		/// Con_SafePrintf
		/// </summary>
		/// <remarks>
		/// Okay to call even when the screen can't be updated
		/// </remarks>
		/// <param name="fmt"></param>
		/// <param name="args"></param>
		public void SafePrint( String fmt, params Object[] args )
		{
			var temp = _videoState.IsScreenDisabledForLoading;
            _videoState.IsScreenDisabledForLoading = true;
			_logger.Print( fmt, args );
            _videoState.IsScreenDisabledForLoading = temp;
		}

		/// <summary>
		/// Con_DrawInput
		/// </summary>
		/// <remarks>
		/// The input line scrolls horizontally if typing goes beyond the right edge
		/// </remarks>
		private void DrawInput( Int32 x, Int32 y )
		{
			if ( _keyboard.Destination != KeyDestination.key_console && !IsForcedUp )
				return;     // don't draw anything

            var t = InputBuffer.ToString( );
            _drawer.DrawString( x, y, t );

			return;

            // add the cursor frame
            _keyboard.Lines[_keyboard.EditLine][_keyboard.LinePos] = ( Char ) ( 10 + ( ( Int32 ) ( Time.Absolute * CursorSpeed ) & 1 ) );

			// fill out remainder with spaces
			for ( var i = _keyboard.LinePos + 1; i < LineWidth; i++ )
				_keyboard.Lines[_keyboard.EditLine][i] = ' ';

			//	prestep if horizontally scrolling
			var offset = 0;
			if ( _keyboard.LinePos >= LineWidth )
				offset = 1 + _keyboard.LinePos - LineWidth;
            //text += 1 + key_linepos - con_linewidth;

            // draw it

            var doubleHeight = _drawer.CharacterAdvance( ) * 2;
            var linesWithPadding = ( _VisLines - doubleHeight );

            var rows = ( Int32 ) Math.Ceiling( linesWithPadding / ( Double ) _drawer.CharacterAdvanceHeight( ) ) - 1;
            //var y = _drawer.CharacterAdvance( ) + ( _drawer.CharacterAdvanceHeight( ) * rows );

            //var yHeight = _drawer.CharacterAdvanceHeight( );
            //var y = _VisLines - ( yHeight * 2 );
   //         var xAdvance = _drawer.CharacterAdvance( );

			//foreach ( var character in InputBuffer.ToString( ) )
   //         {
   //             _drawer.DrawCharacter( xAdvance, y, character );

   //             xAdvance += _drawer.CharacterAdvance( ) + _drawer.MeasureCharacter( character );
   //         }

            //for ( var i = 0; i < LineWidth; i++ )
            //{
            //	var c = _keyboard.Lines[_keyboard.EditLine][offset + i];

            //             _drawer.DrawCharacter( xAdvance, y, c );

            //	xAdvance += _drawer.CharacterAdvance( ) + _drawer.MeasureCharacter( c );
            //         }

            // remove cursor
            _keyboard.Lines[_keyboard.EditLine][_keyboard.LinePos] = '\0';
		}

		// Con_DrawConsole
		//
		// Draws the console with the solid background
		// The typing input line at the bottom should only be drawn if typing is allowed
		public void Draw( Int32 lines, Boolean drawinput )
		{
			if ( lines <= 0 )
				return;

			_VisLines = lines;

            // draw the background
            DrawConsoleBackground( lines );
			
			var conLines = LinesBuffer.ToString( ).Split( '\n' );

            var padding = _drawer.MeasureCharacter( ' ', forceCharset: true ) / 2;
            var xAdvance = padding;
			var displayCount = ( lines - ( padding * 2 ) ) / _drawer.CharacterAdvanceHeight( );
            var yAdvance = padding + ( ( displayCount - 1 ) * _drawer.CharacterAdvanceHeight( ) );

            foreach ( var conLine in conLines.Reverse().Take( displayCount ) )
			{
				_drawer.DrawRichString( xAdvance, yAdvance, conLine, colour: Color.LightGray );

				yAdvance -= _drawer.CharacterAdvanceHeight( );
			}

            yAdvance = padding + ( ( displayCount - 1 ) * _drawer.CharacterAdvanceHeight( ) );

            if ( drawinput )
                DrawInput( xAdvance, yAdvance );

            //// draw the text
            //_VisLines = lines;

            //var doubleHeight = _drawer.CharacterAdvance( ) * 2;
            //var linesWithPadding = ( lines - doubleHeight );

            //         var rows = ( Int32 ) Math.Ceiling( linesWithPadding / ( Double ) _drawer.CharacterAdvanceHeight( ) ) - 1;
            //var y = _drawer.CharacterAdvance( );
            //         //var rows = ( lines - doubleHeight ) >> 3;     // rows of text to draw
            //         //var y = lines - doubleHeight - ( rows << 3 ); // may start slightly negative

            //         for ( var i = Current - rows + 1; i <= Current; i++, y += _drawer.CharacterAdvanceHeight( ) )
            //{
            //	var j = i - BackScroll;
            //	if ( j < 0 )
            //		j = 0;

            //	var offset = ( j % TotalLines ) * LineWidth;

            //	var xAdvance = _drawer.CharacterAdvance( );

            //	for ( var x = 0; x < LineWidth; x++ )
            //	{
            //		var c = Text[offset + x];
            //                 _drawer.DrawCharacter( xAdvance, y, c );

            //		xAdvance += _drawer.CharacterAdvance() + _drawer.MeasureCharacter( c );
            //             }
            //}

            //// draw the input prompt, user text, and cursor if desired
            //if ( drawinput )
            //	DrawInput( );
        }

        /// <summary>
        /// Draw_ConsoleBackground
        /// </summary>
        /// <param name="lines"></param>
        public void DrawConsoleBackground( Int32 lines )
        {
            var padding = _drawer.MeasureCharacter( ' ', forceCharset: true ) / 2;
            var displayCount = ( lines - ( padding * 2 ) ) / _drawer.CharacterAdvanceHeight( );
            var height = ( padding  * 2 ) + ( displayCount * _drawer.CharacterAdvanceHeight( ) );

            var y = ( _videoState.Data.height * 3 ) >> 2;

            var newUI = Cvars.NewUI.Get<Boolean>( );
			
			if ( lines > y )
			{
				if ( newUI )
				{
					_video.Device.Graphics.Fill( 0, lines - _videoState.Data.height, _videoState.Data.width, _videoState.Data.height, HudResources.NEWUI_BG );
				}
				else
				{
					_video.Device.Graphics.DrawPicture( ConsoleBackground, 0, lines - _videoState.Data.height, _videoState.Data.width, _videoState.Data.height );
				}
			}
			else
			{
				var alpha = ( Int32 ) Math.Min( ( 255 * ( ( 1.2f * lines ) / y ) ), 255 );

                var aspectRatio = ConsoleBackground.Height / ( float )ConsoleBackground.Width;
				var bgheight = ( Int32 ) ( _videoState.Data.width * aspectRatio );

				if ( newUI )
				{
					_video.Device.Graphics.Fill( 0, height - bgheight, _videoState.Data.width, bgheight, HudResources.NEWUI_BG );
					_video.Device.Graphics.Fill( 0, height + 2, _videoState.Data.width, 2, HudResources.NEWUI_BORDER );
				}
				else
				{
					_video.Device.Graphics.DrawPicture( ConsoleBackground, 0, height - bgheight, _videoState.Data.width, bgheight, Color.FromArgb( alpha, Color.White ) );
				}
			}
		}

		public void InitialiseBackground( )
		{
			var ver = String.Format( $"(c# {QDef.CSQUAKE_VERSION,7:F2}) {QDef.VERSION,7:F2}" );

			var usingTTF = Cvars.TrueTypeFonts.Get<Boolean>( );
            Background = new TextPicture( _drawer, _video, "gfx/conback.lmp", "GL_NEAREST", !usingTTF, isBigFont: usingTTF );
			Background.DrawString( ver, Colours.Quake, Menus.TextAlignment.Right, VerticalAlighment.Bottom );
            ConsoleBackground = Background.Build( );

            IsInitialised = true;
        }

		private void CharToConback( Int32 num, ByteArraySegment dest, ByteArraySegment drawChars )
		{
			var row = num >> 4;
			var col = num & 15;
			var destOffset = dest.StartIndex;
			var srcOffset = drawChars.StartIndex + ( row << 10 ) + ( col << 3 );
			//source = draw_chars + (row<<10) + (col<<3);
			var drawline = 8;

			while ( drawline-- > 0 )
			{
				for ( var x = 0; x < 8; x++ )
					if ( drawChars.Data[srcOffset + x] != 255 )
						dest.Data[destOffset + x] = ( Byte ) ( 0x60 + drawChars.Data[srcOffset + x] ); // source[x];
				srcOffset += 128; // source += 128;
				destOffset += 320; // dest += 320;
			}
		}

		/// <summary>
		/// Moved from Keyboard to remove keyboard dependency on visual console
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		private Boolean KeyConsoleScroll( Int32 key )
		{
			if ( key == KeysDef.K_PGUP || key == KeysDef.K_MWHEELUP )
			{
				BackScroll += 2;
				if ( BackScroll > TotalLines - ( _videoState.Data.height >> 3 ) - 1 )
					BackScroll = TotalLines - ( _videoState.Data.height >> 3 ) - 1;
				return true;
			}

			if ( key == KeysDef.K_PGDN || key == KeysDef.K_MWHEELDOWN )
			{
				BackScroll -= 2;
				if ( BackScroll < 0 )
					BackScroll = 0;
				return true;
			}

			if ( key == KeysDef.K_HOME )
			{
				BackScroll = TotalLines - ( _videoState.Data.height >> 3 ) - 1;
				return true;
			}

			if ( key == KeysDef.K_END )
			{
				BackScroll = 0;
				return true;
			}

			return false;
		}

        private Boolean KeyConsoleEnter( )
        {
			if ( InputBuffer.Length > 1 ) 
			{ 
				var line = InputBuffer.ToString().Trim( );

				var cmd = line.Substring( 1 );

				_commands.Buffer.Append( cmd );	// skip the >
				_commands.Buffer.Append( "\n" );

				_logger.Print( "{0}\n", line );

                History.Add( cmd );
                HistoryPosition = History.Count;

                ResetInput( );

                if ( _clientState.StaticData.state == cactive_t.ca_disconnected )
					_screen.UpdateScreen( ); // force an update, because the command
            }							 // may take some time
            return true;
        }

		private void UpdateAutocomplete()
		{
            Autocomplete.Clear( );

            // command completion
            var txt = InputBuffer.ToString( ).Substring( 1 ).Trim( );

			if ( String.IsNullOrEmpty( txt ) )
				return;

            var cmds = _commands.Complete( txt );
            var vars = _cvars.CompleteName( txt );

            if ( cmds?.Length > 0 )
            {
				foreach ( var s in cmds )
                    Autocomplete.Add( s );
            }

            if ( vars?.Length > 0 )
            {
                foreach ( var s in vars )
                    Autocomplete.Add( s );
            }
        }

        private Boolean KeyConsoleTab( )
        {
			Autocomplete.Clear( );

            // command completion
            var txt = InputBuffer.ToString().Trim().Substring( 1 );
            var cmds = _commands.Complete( txt );
            var vars = _cvars.CompleteName( txt );
            String match = null;

            if ( cmds != null )
            {
                if ( cmds.Length > 1 )
                {
					foreach ( var s in cmds )
						Autocomplete.Add( s );
                }
                else
                    match = cmds[0];
            }

            if ( match == null && vars != null )
            {
                if ( vars.Length > 1 || cmds != null )
                {
                    foreach ( var s in vars )
                        Autocomplete.Add( s );
                }
                else
                    match = vars[0];
            }

            if ( !String.IsNullOrEmpty( match ) )
            {
				ResetInput( );

				InputBuffer.Append( match );

				MoveCursorToEnd( );

                return true;
            }

            return false;
        }

        private Boolean KeyConsoleArrows( Int32 key )
        {
            if ( key == KeysDef.K_UPARROW )
            {
				if ( HistoryPosition > 0 )
				{
					HistoryPosition--;

					var historyLine = History[HistoryPosition];

					if ( PREFIX + historyLine != InputBuffer.ToString( ) )
					{
                        ResetInput( );

                        InputBuffer.Append( historyLine );

                        MoveCursorToEnd( );
                    }
				}
                return true;
            }

            if ( key == KeysDef.K_DOWNARROW )
            {
				if ( HistoryPosition < ( History.Count - 1 ) )
				{
					HistoryPosition++;

					var historyLine = History[HistoryPosition];

					if ( PREFIX + historyLine != InputBuffer.ToString( ) )
                    {
                        ResetInput( );

                        InputBuffer.Append( historyLine );

                        MoveCursorToEnd( );
                    }
				}
				else
                    ResetInput( );

                return true;
            }

            return false;
        }

		private void KeyBackspace()
		{
            if ( InputBuffer.Length <= 1 )
				return;

            InputBuffer.Remove( InputBuffer.Length - 1, 1 );
            MoveCursorToEnd( );
			UpdateAutocomplete( );
        }

        /// <summary>
        /// Key_Console
        /// Interactive line editing and console scrollback
        /// </summary>
        private void KeyConsole( Int32 key )
        {
            if ( key == KeysDef.K_ENTER && KeyConsoleEnter( ) )
                return;

            if ( key == KeysDef.K_TAB && KeyConsoleTab( ) )
                return;

			if ( key == KeysDef.K_BACKSPACE )
			{
				KeyBackspace( );
                return;
            }

            if ( key == KeysDef.K_LEFTARROW && CursorPosition > 1 )
            {
				MoveCursorBack( );
                return;
            }

            if ( KeyConsoleArrows( key ) || KeyConsoleScroll( key ) )
                return;

            if ( key < 32 || key > 127 )
                return;	// non printable

            if ( InputBuffer.Length < KeysDef.MAXCMDLINE )
            {
				InputBuffer.Append( ( Char ) key );
				UpdateAutocomplete( );
                MoveCursorToEnd( );
            }
        }

		/// <summary>
		/// Resets the console input buffer and cursor
		/// </summary>
		private void ResetInput( )
        {
            InputBuffer
				.Clear( )
				.Append( PREFIX );

            MoveCursorToEnd( );
			UpdateAutocomplete( );
        }

		/// <summary>
		/// Clear the notifcation wall
		/// </summary>
		private void ResetNotificationWall()
		{
			NotificationWall.Clear( );
		}

        private void MoveCursorToEnd( )
        {
            CursorPosition = InputBuffer.Length - 1;
        }

        private void MoveCursorBack( )
        {
            CursorPosition--;
        }

        /// <summary>
        /// Con_ToggleConsole_f
        /// </summary>
        public void ToggleConsole_f( CommandMessage msg )
        {
            if ( _keyboard.Destination == KeyDestination.key_console )
            {
                if ( _clientState.StaticData.state == cactive_t.ca_connected )
                {
                    _keyboard.Destination = KeyDestination.key_game;
                    _keyboard.Lines[_keyboard.EditLine][1] = '\0';  // clear any typing
                    _keyboard.LinePos = 1;
                }
                else
                    _menus.Show( "menu_main" );
            }
            else
                _keyboard.Destination = KeyDestination.key_console;

            _screen.EndLoadingPlaque( );

			ResetNotificationWall( );
            ResetInput( );
            //ToggleMenu_f( null );
        }
    }
}
