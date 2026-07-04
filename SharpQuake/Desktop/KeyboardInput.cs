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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SharpQuake.Framework;
using SharpQuake.Framework.Factories.IO;
using SharpQuake.Framework.IO;
using SharpQuake.Framework.IO.Input;
using SharpQuake.Framework.Logging;

// keys.h
// keys.c

// key up events are sent even if in console mode

namespace SharpQuake.Desktop
{
    /// <summary>
    /// Key_functions
    /// </summary>
    public class KeyboardInput : IKeyboardInput
    {
        private List<(KeyDestination Destination, Action<Int32> OnKeyPressed)> Listeners
        {
            get;
            set;
        } = new List<(KeyDestination, Action<Int32>)>( );

        /// <summary>
        /// Used to prevent input whilst a demo is playing
        /// </summary>
        public Boolean IsWatchingDemo
        {
            get;
            set;
        }

        public KeyDestination Destination
        {
            get;
            set;
        }

        public Boolean TeamMessage
        {
            get;
            set;
        }

        public Char[][] Lines
        {
            get;
            set;
        } = new Char[32][];//, MAXCMDLINE]; // char	keyLines[32][MAXCMDLINE];

        public Int32 EditLine
        {
            get;
            set;
        }

        public StringBuilder ChatBuffer
        {
            get;
            set;
        } = new StringBuilder( 32 ); // chat_buffer

        public Int32 LastPress
        {
            get;
            set;
        }

        public String[] Bindings
        {
            get;
            set;
        } = new String[256]; // char	*keybindings[256];

        public Int32 LinePos
        {
            get;
            set;
        }

        public Int32 KeyCount
        {
            get;
            set;
        }

        // key_linepos
        private Boolean _ShiftDown; // = false;


        // key_count			// incremented every key event

        private Boolean[] _ConsoleKeys = new Boolean[256]; // consolekeys[256]	// if true, can't be rebound while in console
        private Boolean[] _MenuBound = new Boolean[256]; // menubound[256]	// if true, can't be rebound while in menu
        private Int32[] _KeyShift = new Int32[256]; // keyshift[256]		// key to map to if shift held down in console
        private Int32[] Repeats = new Int32[256]; // keyRepeats[256]	// if > 1, it is autorepeating
        private Boolean[] _KeyDown = new Boolean[256];

        private readonly IConsoleLogger _logger;
        private readonly CommandFactory _commands;

        public KeyboardInput( CommandFactory commands, IConsoleLogger logger )
        {
            _commands = commands;
            _logger = logger;

            // TODO - Move this somewhere else
            Listen( KeyDestination.key_message, KeyMessage );
        }

        // Key_Event (int key, qboolean down)
        //
        // Called by the system between frames for both key up and key down events
        // Should NOT be called during an interrupt!
        public void Event( Int32 key, Boolean down )
        {
            _KeyDown[key] = down;

            if ( !down )
                Repeats[key] = 0;

            LastPress = key;
            KeyCount++;
            if ( KeyCount <= 0 )
                return;     // just catching keys for Con_NotifyBox

            // update auto-repeat status
            if ( down )
            {
                Repeats[key]++;
                if ( key != KeysDef.K_BACKSPACE && key != KeysDef.K_PAUSE && key != KeysDef.K_PGUP && key != KeysDef.K_PGDN && Repeats[key] > 1 )
                {
                    return; // ignore most autorepeats
                }

                if ( key >= 200 && String.IsNullOrEmpty( Bindings[key] ) )
                    _logger.Print( "{0} is unbound, hit F4 to set.\n", KeynumToString( key ) );
            }

            if ( key == KeysDef.K_SHIFT )
                _ShiftDown = down;

            //
            // handle escape specialy, so the user can never unbind it
            //
            if ( key == KeysDef.K_ESCAPE )
            {
                if ( !down )
                    return;

                PropagateEvents( key );
                //switch ( Destination )
                //{
                //    case KeyDestination.key_message:
                //        KeyMessage( key );
                //        break;

                //    case KeyDestination.key_menu:
                //        OnMenuKeyDown?.Invoke( key );
                //        break;

                //    case KeyDestination.key_game:
                //    case KeyDestination.key_console:
                //        OnToggleConsole?.Invoke( );
                //        break;

                //    default:
                //        Utilities.Error( "Bad key_dest" );
                //        break;
                //}
                return;
            }

            //
            // key up events only generate commands if the game key binding is
            // a button command (leading + sign).  These will occur even in console mode,
            // to keep the character from continuing an action started before a console
            // switch.  Button commands include the keynum as a parameter, so multiple
            // downs can be matched with ups
            //
            if ( !down )
            {
                var kb = Bindings[key];

                if ( !String.IsNullOrEmpty( kb ) && kb.StartsWith( "+" ) )
                {
                    _commands.Buffer.Append( String.Format( "-{0} {1}\n", kb.Substring( 1 ), key ) );
                }

                if ( _KeyShift[key] != key )
                {
                    kb = Bindings[_KeyShift[key]];
                    if ( !String.IsNullOrEmpty( kb ) && kb.StartsWith( "+" ) )
                        _commands.Buffer.Append( String.Format( "-{0} {1}\n", kb.Substring( 1 ), key ) );
                }
                return;
            }

            //
            // during demo playback, most keys bring up the main menu
            //
            if ( IsWatchingDemo && down && _ConsoleKeys[key] && Destination == KeyDestination.key_game )
            {
                PropagateEvents( key );
                //OnToggleConsole?.Invoke( );
                return;
            }

            //
            // if not a consolekey, send to the interpreter no matter what mode is
            //
            if ( ( Destination == KeyDestination.key_menu && _MenuBound[key] ) ||
                ( Destination == KeyDestination.key_console && !_ConsoleKeys[key] ) ||
                ( Destination == KeyDestination.key_game && ( !_logger.IsForcedUp || !_ConsoleKeys[key] ) ) )
            {
                var kb = Bindings[key];
                if ( !String.IsNullOrEmpty( kb ) )
                {
                    if ( kb.StartsWith( "+" ) )
                    {
                        // button commands add keynum as a parm
                        _commands.Buffer.Append( String.Format( "{0} {1}\n", kb, key ) );
                    }
                    else
                    {
                        _commands.Buffer.Append( kb );
                        _commands.Buffer.Append( "\n" );
                    }
                }
                return;
            }

            if ( !down )
                return;     // other systems only care about key down events

            if ( _ShiftDown )
            {
                key = _KeyShift[key];
            }

            PropagateEvents( key );

            //switch ( Destination )
            //{
            //    case KeyDestination.key_message:
            //        KeyMessage( key );
            //        break;

            //    case KeyDestination.key_menu:
            //        OnMenuKeyDown?.Invoke( key );
            //        break;

            //    case KeyDestination.key_game:
            //    case KeyDestination.key_console:
            //        KeyConsole( key );
            //        break;

            //    default:
            //        Utilities.Error( "Bad key_dest" );
            //        break;
            //}
        }

        private void PropagateEvents( Int32 key )
        {
            var destinationListeners = Listeners.Where( l => l.Destination == Destination ).ToList( );

            if ( destinationListeners.Count > 0 )
            {
                foreach ( var listener in destinationListeners )
                    listener.OnKeyPressed?.Invoke( key );
            }
            else
                Utilities.Error( "Bad key_dest" );
        }

        // Key_Init (void);
        public void Initialise( )
        {
            for ( var i = 0; i < 32; i++ )
            {
                Lines[i] = new Char[KeysDef.MAXCMDLINE];
                Lines[i][0] = ']'; // keyLines[i][0] = ']'; keyLines[i][1] = 0;
            }

            LinePos = 1;

            //
            // init ascii characters in console mode
            //
            for ( var i = 32; i < 128; i++ )
                _ConsoleKeys[i] = true;

            _ConsoleKeys[KeysDef.K_ENTER] = true;
            _ConsoleKeys[KeysDef.K_TAB] = true;
            _ConsoleKeys[KeysDef.K_LEFTARROW] = true;
            _ConsoleKeys[KeysDef.K_RIGHTARROW] = true;
            _ConsoleKeys[KeysDef.K_UPARROW] = true;
            _ConsoleKeys[KeysDef.K_DOWNARROW] = true;
            _ConsoleKeys[KeysDef.K_BACKSPACE] = true;
            _ConsoleKeys[KeysDef.K_PGUP] = true;
            _ConsoleKeys[KeysDef.K_PGDN] = true;
            _ConsoleKeys[KeysDef.K_SHIFT] = true;
            _ConsoleKeys[KeysDef.K_MWHEELUP] = true;
            _ConsoleKeys[KeysDef.K_MWHEELDOWN] = true;
            _ConsoleKeys['`'] = false;
            _ConsoleKeys['~'] = false;

            for ( var i = 0; i < 256; i++ )
                _KeyShift[i] = i;
            for ( Int32 i = 'a'; i <= 'z'; i++ )
                _KeyShift[i] = i - 'a' + 'A';
            _KeyShift['1'] = '!';
            _KeyShift['2'] = '@';
            _KeyShift['3'] = '#';
            _KeyShift['4'] = '$';
            _KeyShift['5'] = '%';
            _KeyShift['6'] = '^';
            _KeyShift['7'] = '&';
            _KeyShift['8'] = '*';
            _KeyShift['9'] = '(';
            _KeyShift['0'] = ')';
            _KeyShift['-'] = '_';
            _KeyShift['='] = '+';
            _KeyShift[','] = '<';
            _KeyShift['.'] = '>';
            _KeyShift['/'] = '?';
            _KeyShift[';'] = ':';
            _KeyShift['\''] = '"';
            _KeyShift['['] = '{';
            _KeyShift[']'] = '}';
            _KeyShift['`'] = '~';
            _KeyShift['\\'] = '|';

            _MenuBound[KeysDef.K_ESCAPE] = true;
            for ( var i = 0; i < 12; i++ )
                _MenuBound[KeysDef.K_F1 + i] = true;

            //
            // register our functions
            //
            _commands.Add( "bind", Bind_f );
            _commands.Add( "unbind", Unbind_f );
            _commands.Add( "unbindall", UnbindAll_f );
        }

        public Boolean IsValidConsoleCharacter( Char character )
        {
            if ( character < 32 || character >= 128 )
                return false;

            return _ConsoleKeys[character];
        }

        public Boolean IsKeyDown( Int32 key )
        {
            return _KeyDown[key];
        }

        /// <summary>
        /// Key_WriteBindings
        /// </summary>
        public void WriteBindings( Stream dest )
        {
            var sb = new StringBuilder( 4096 );
            for ( var i = 0; i < 256; i++ )
            {
                if ( !String.IsNullOrEmpty( Bindings[i] ) )
                {
                    sb.Append( "bind \"" );
                    sb.Append( KeynumToString( i ) );
                    sb.Append( "\" \"" );
                    sb.Append( Bindings[i] );
                    sb.AppendLine( "\"" );
                }
            }
            var buf = Encoding.ASCII.GetBytes( sb.ToString( ) );
            dest.Write( buf, 0, buf.Length );
        }

        /// <summary>
        /// Key_SetBinding
        /// </summary>
        public void SetBinding( Int32 keynum, String binding )
        {
            if ( keynum != -1 )
            {
                Bindings[keynum] = binding;
            }
        }

        // Key_ClearStates (void)
        public void ClearStates( )
        {
            for ( var i = 0; i < 256; i++ )
            {
                _KeyDown[i] = false;
                Repeats[i] = 0;
            }
        }

        // Key_KeynumToString
        //
        // Returns a string (either a single ascii char, or a K_* name) for the
        // given keynum.
        // FIXME: handle quote special (general escape sequence?)
        public String KeynumToString( Int32 keynum )
        {
            if ( keynum == -1 )
                return "<KEY NOT FOUND>";

            if ( keynum > 32 && keynum < 127 )
            {
                // printable ascii
                return ( ( Char ) keynum ).ToString( );
            }

            foreach ( var kn in KeysDef.KeyNames )
            {
                if ( kn.keynum == keynum )
                    return kn.name;
            }
            return "<UNKNOWN KEYNUM>";
        }

        // Key_StringToKeynum
        //
        // Returns a key number to be used to index keybindings[] by looking at
        // the given string.  Single ascii characters return themselves, while
        // the K_* names are matched up.
        private Int32 StringToKeynum( String str )
        {
            if ( String.IsNullOrEmpty( str ) )
                return -1;
            if ( str.Length == 1 )
                return str[0];

            foreach ( var keyname in KeysDef.KeyNames )
            {
                if ( Utilities.SameText( keyname.name, str ) )
                    return keyname.keynum;
            }
            return -1;
        }

        //Key_Unbind_f
        private void Unbind_f( CommandMessage msg )
        {
            var c = msg.Parameters != null ? msg.Parameters.Length : 0;

            if ( c != 1 )
            {
                _logger.Print( "unbind <key> : remove commands from a key\n" );
                return;
            }

            var b = StringToKeynum( msg.Parameters[0] );
            if ( b == -1 )
            {
                _logger.Print( $"\"{msg.Parameters[0]}\" isn't a valid key\n" );
                return;
            }

            SetBinding( b, null );
        }

        // Key_Unbindall_f
        private void UnbindAll_f( CommandMessage msg )
        {
            for ( var i = 0; i < 256; i++ )
                if ( !String.IsNullOrEmpty( Bindings[i] ) )
                    SetBinding( i, null );
        }

        //Key_Bind_f
        private void Bind_f( CommandMessage msg )
        {
            var c = msg.Parameters != null ? msg.Parameters.Length : 0;
            if ( c != 1 && c != 2 )
            {
                _logger.Print( "bind <key> [command] : attach a command to a key\n" );
                return;
            }

            var b = StringToKeynum( msg.Parameters[0] );
            if ( b == -1 )
            {
                _logger.Print( $"\"{msg.Parameters[0]}\" isn't a valid key\n" );
                return;
            }

            if ( c == 1 )
            {
                if ( !String.IsNullOrEmpty( Bindings[b] ) )// keybindings[b])
                    _logger.Print( $"\"{msg.Parameters[0]}\" = \"{Bindings[b]}\"\n" );
                else
                    _logger.Print( $"\"{msg.Parameters[0]}\" is not bound\n" );
                return;
            }

            // copy the rest of the command line
            // start out with a null string

            var args = String.Empty;

            if ( msg.Parameters.Length > 1 )
                args = msg.ParametersFrom( 1 );

            SetBinding( b, args );
        }

        // Key_Message (int key)
        private void KeyMessage( Int32 key )
        {
            if ( key == KeysDef.K_ENTER )
            {
                if ( TeamMessage )
                    _commands.Buffer.Append( "say_team \"" );
                else
                    _commands.Buffer.Append( "say \"" );

                _commands.Buffer.Append( ChatBuffer.ToString( ) );
                _commands.Buffer.Append( "\"\n" );

                Destination = KeyDestination.key_game;
                ChatBuffer.Length = 0;
                return;
            }

            if ( key == KeysDef.K_ESCAPE )
            {
                Destination = KeyDestination.key_game;
                ChatBuffer.Length = 0;
                return;
            }

            if ( key < 32 || key > 127 )
                return;	// non printable

            if ( key == KeysDef.K_BACKSPACE )
            {
                if ( ChatBuffer.Length > 0 )
                {
                    ChatBuffer.Length--;
                }
                return;
            }

            if ( ChatBuffer.Length == 31 )
                return; // all full

            ChatBuffer.Append( ( Char ) key );
        }

        //private Boolean KeyConsoleEnter( )
        //{
        //    var line = new String( Lines[EditLine] ).TrimEnd( '\0', ' ' );
        //    var cmd = line.Substring( 1 );
        //    _commands.Buffer.Append( cmd );	// skip the >
        //    _commands.Buffer.Append( "\n" );
        //    _logger.Print( "{0}\n", line );
        //    EditLine = ( EditLine + 1 ) & 31;
        //    _HistoryLine = EditLine;
        //    Lines[EditLine][0] = ']';
        //    LinePos = 1;

        //    OnConsoleSubmit?.Invoke( ); // Propagate event for console submit, used for screen refresh

        //    return true;
        //}

        //private Boolean KeyConsoleTab( )
        //{
        //    // command completion
        //    var txt = new String( Lines[EditLine], 1, KeysDef.MAXCMDLINE - 1 ).TrimEnd( '\0', ' ' );
        //    var cmds = _commands.Complete( txt );
        //    var vars = _cvars.CompleteName( txt );
        //    String match = null;
        //    if ( cmds != null )
        //    {
        //        if ( cmds.Length > 1 || vars != null )
        //        {
        //            _logger.Print( "\nCommands:\n" );
        //            foreach ( var s in cmds )
        //                _logger.Print( "  {0}\n", s );
        //        }
        //        else
        //            match = cmds[0];
        //    }
        //    if ( vars != null )
        //    {
        //        if ( vars.Length > 1 || cmds != null )
        //        {
        //            _logger.Print( "\nVariables:\n" );
        //            foreach ( var s in vars )
        //                _logger.Print( "  {0}\n", s );
        //        }
        //        else if ( match == null )
        //            match = vars[0];
        //    }
        //    if ( !String.IsNullOrEmpty( match ) )
        //    {
        //        var len = Math.Min( match.Length, KeysDef.MAXCMDLINE - 3 );
        //        for ( var i = 0; i < len; i++ )
        //        {
        //            Lines[EditLine][i + 1] = match[i];
        //        }
        //        LinePos = len + 1;
        //        Lines[EditLine][LinePos] = ' ';
        //        LinePos++;
        //        Lines[EditLine][LinePos] = '\0';
        //        return true;
        //    }

        //    return false;
        //}

        //private Boolean KeyConsoleArrows( Int32 key )
        //{
        //    if ( key == KeysDef.K_UPARROW )
        //    {
        //        do
        //        {
        //            _HistoryLine = ( _HistoryLine - 1 ) & 31;
        //        } while ( _HistoryLine != EditLine && ( Lines[_HistoryLine][1] == 0 ) );
        //        if ( _HistoryLine == EditLine )
        //            _HistoryLine = ( EditLine + 1 ) & 31;
        //        Array.Copy( Lines[_HistoryLine], Lines[EditLine], KeysDef.MAXCMDLINE );
        //        LinePos = 0;
        //        while ( Lines[EditLine][LinePos] != '\0' && LinePos < KeysDef.MAXCMDLINE )
        //            LinePos++;
        //        return true;
        //    }

        //    if ( key == KeysDef.K_DOWNARROW )
        //    {
        //        if ( _HistoryLine == EditLine )
        //            return true;
        //        do
        //        {
        //            _HistoryLine = ( _HistoryLine + 1 ) & 31;
        //        }
        //        while ( _HistoryLine != EditLine && ( Lines[_HistoryLine][1] == '\0' ) );
        //        if ( _HistoryLine == EditLine )
        //        {
        //            Lines[EditLine][0] = ']';
        //            LinePos = 1;
        //        }
        //        else
        //        {
        //            Array.Copy( Lines[_HistoryLine], Lines[EditLine], KeysDef.MAXCMDLINE );
        //            LinePos = 0;
        //            while ( Lines[EditLine][LinePos] != '\0' && LinePos < KeysDef.MAXCMDLINE )
        //                LinePos++;
        //        }
        //        return true;
        //    }

        //    return false;
        //}

        ///// <summary>
        ///// Redirect console scrolling to the visual console element
        ///// </summary>
        ///// <param name="key"></param>
        ///// <returns></returns>
        //private Boolean KeyConsoleScroll( Int32 key )
        //{
        //    if ( OnConsoleScroll != null )
        //        return OnConsoleScroll.Invoke( key );

        //    return false;
        //}

        ///// <summary>
        ///// Key_Console
        ///// Interactive line editing and console scrollback
        ///// </summary>
        //private void KeyConsole( Int32 key )
        //{
        //    if ( key == KeysDef.K_ENTER && KeyConsoleEnter( ) )
        //        return;

        //    if ( key == KeysDef.K_TAB && KeyConsoleTab() )
        //        return;            

        //    if ( key == KeysDef.K_BACKSPACE || key == KeysDef.K_LEFTARROW )
        //    {
        //        if ( LinePos > 1 )
        //            LinePos--;
        //        return;
        //    }

        //    if ( KeyConsoleArrows( key ) || KeyConsoleScroll( key ) )
        //        return;         

        //    if ( key < 32 || key > 127 )
        //        return;	// non printable

        //    if ( LinePos < KeysDef.MAXCMDLINE - 1 )
        //    {
        //        Lines[EditLine][LinePos] = ( Char ) key;
        //        LinePos++;
        //        Lines[EditLine][LinePos] = '\0';
        //    }
        //}


        public void Listen( KeyDestination destination, Action<Int32> onKeyPressed )
        {
            Listeners.Add( (destination, onKeyPressed) );
        }
    }

    // keydest_t;
}
