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
using System;
using SharpQuake.Framework.Logging;

namespace SharpQuake.Sys.Programs
{
    public class ProgramErrorHandler
    {
        public static readonly String[] OPERATOR_NAMES = new String[]
        {
            "DONE",

            "MUL_F",
            "MUL_V",
            "MUL_FV",
            "MUL_VF",

            "DIV",

            "ADD_F",
            "ADD_V",

            "SUB_F",
            "SUB_V",

            "EQ_F",
            "EQ_V",
            "EQ_S",
            "EQ_E",
            "EQ_FNC",

            "NE_F",
            "NE_V",
            "NE_S",
            "NE_E",
            "NE_FNC",

            "LE",
            "GE",
            "LT",
            "GT",

            "INDIRECT",
            "INDIRECT",
            "INDIRECT",
            "INDIRECT",
            "INDIRECT",
            "INDIRECT",

            "ADDRESS",

            "STORE_F",
            "STORE_V",
            "STORE_S",
            "STORE_ENT",
            "STORE_FLD",
            "STORE_FNC",

            "STOREP_F",
            "STOREP_V",
            "STOREP_S",
            "STOREP_ENT",
            "STOREP_FLD",
            "STOREP_FNC",

            "RETURN",

            "NOT_F",
            "NOT_V",
            "NOT_S",
            "NOT_ENT",
            "NOT_FNC",

            "IF",
            "IFNOT",

            "CALL0",
            "CALL1",
            "CALL2",
            "CALL3",
            "CALL4",
            "CALL5",
            "CALL6",
            "CALL7",
            "CALL8",

            "STATE",

            "GOTO",

            "AND",
            "OR",

            "BITAND",
            "BITOR"
        };

        private readonly IConsoleLogger _logger;
        private readonly IEngine _engine;
        private readonly ProgramsState _state;

        public ProgramErrorHandler( IConsoleLogger logger, IEngine engine, ProgramsState state )
        {
            _logger = logger;
            _engine = engine;
            _state = state;
        }

        /// <summary>
        /// PR_StackTrace
        /// </summary>
        private void StackTrace( )
        {
            if ( _state.Depth == 0 )
            {
                _logger.Print( "<NO STACK>\n" );
                return;
            }

            _state.Stack[_state.Depth].f = _state.xFunction;
            for ( var i = _state.Depth; i >= 0; i-- )
            {
                var f = _state.Stack[i].f;

                if ( f == null )
                {
                    _logger.Print( "<NO FUNCTION>\n" );
                }
                else
                    _logger.Print( "{0,12} : {1}\n", _state.GetString( f.s_file ), _state.GetString( f.s_name ) );
            }
        }

        /// <summary>
        /// PR_PrintStatement
        /// </summary>
        public void PrintStatement( ref Statement s )
        {
            if ( s.op < OPERATOR_NAMES.Length )
            {
                _logger.Print( "{0,10} ", OPERATOR_NAMES[s.op] );
            }

            var op = ( ProgramOperator ) s.op;
            if ( op == ProgramOperator.OP_IF || op == ProgramOperator.OP_IFNOT )
                _logger.Print( "{0}branch {1}", _state.GlobalString( s.a ), s.b );
            else if ( op == ProgramOperator.OP_GOTO )
            {
                _logger.Print( "branch {0}", s.a );
            }
            else if ( ( UInt32 ) ( s.op - ProgramOperator.OP_STORE_F ) < 6 )
            {
                _logger.Print( _state.GlobalString( s.a ) );
                _logger.Print( _state.GlobalStringNoContents( s.b ) );
            }
            else
            {
                if ( s.a != 0 )
                    _logger.Print( _state.GlobalString( s.a ) );
                if ( s.b != 0 )
                    _logger.Print( _state.GlobalString( s.b ) );
                if ( s.c != 0 )
                    _logger.Print( _state.GlobalStringNoContents( s.c ) );
            }
            _logger.Print( "\n" );
        }

        /// <summary>
        /// PR_RunError
        /// Aborts the currently executing function
        /// </summary>
        public void RunError( String fmt, params Object[] args )
        {
            PrintStatement( ref _state.Statements[_state.xStatement] );
            StackTrace( );
            _logger.Print( fmt, args );

            _state.Depth = 0;		// dump the stack so host_error can shutdown functions

            _engine.Error( "Program error" );
        }

        public void Error( String error, params Object[] args )
        {
            _engine.Error( error, args );
        }
    }
}
