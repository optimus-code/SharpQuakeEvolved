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
using SharpQuake.Framework.Factories.IO;
using SharpQuake.Framework.IO;
using SharpQuake.Framework.Logging;
using SharpQuake.Networking.Server;
using System;

namespace SharpQuake.Sys.Programs
{
    public class ProgramsExec

    {
        private readonly IEngine _engine;
        private readonly IConsoleLogger _logger;
        private readonly CommandFactory _commands;
        private readonly ClientVariableFactory _cvars;
        private readonly ProgramsState _state;
        private readonly ProgramErrorHandler _programErrors;
        private readonly ServerState _serverState;

        public ProgramsExec( IEngine engine, IConsoleLogger logger, CommandFactory commands,
            ClientVariableFactory cvars, ProgramsState state, 
            ProgramErrorHandler programErrors, ServerState serverState )
        {
            _engine = engine;
            _logger = logger;
            _commands = commands;
            _cvars = cvars;
            _state = state;
            _state.OnExecute += Execute;
            _programErrors = programErrors;
            _serverState = serverState;
        }

        /// <summary>
        /// PR_ExecuteProgram
        /// </summary>
        public unsafe void Execute( Int32 fnum )
        {
            if ( fnum < 1 || fnum >= _state.Functions.Length )
            {
                if ( _state.GlobalStruct.self != 0 )
                    _state.Print( _serverState.ProgToEdict( _state.GlobalStruct.self ) );

                _engine.Error( "PR_ExecuteProgram: NULL function" );
            }

            var f = _state.Functions[fnum];

            var runaway = 100000;
            _state.Trace = false;

            // make a stack frame
            var exitdepth = _state.Depth;

            Int32 ofs;
            var s = EnterFunction( f );
            MemoryEdict ed;

            while ( true )
            {
                s++;	// next statement

                var a = ( EVal* ) _state.Get( _state.Statements[s].a );
                var b = ( EVal* ) _state.Get( _state.Statements[s].b );
                var c = ( EVal* ) _state.Get( _state.Statements[s].c );

                if ( --runaway == 0 )
                    _programErrors.RunError( "runaway loop error" );

                _state.xFunction.profile++;
                _state.xStatement = s;

                if ( _state.Trace )
                    _programErrors.PrintStatement( ref _state.Statements[s] );

                switch ( ( ProgramOperator ) _state.Statements[s].op )
                {
                    case ProgramOperator.OP_ADD_F:
                        c->_float = a->_float + b->_float;
                        break;

                    case ProgramOperator.OP_ADD_V:
                        c->vector[0] = a->vector[0] + b->vector[0];
                        c->vector[1] = a->vector[1] + b->vector[1];
                        c->vector[2] = a->vector[2] + b->vector[2];
                        break;

                    case ProgramOperator.OP_SUB_F:
                        c->_float = a->_float - b->_float;
                        break;

                    case ProgramOperator.OP_SUB_V:
                        c->vector[0] = a->vector[0] - b->vector[0];
                        c->vector[1] = a->vector[1] - b->vector[1];
                        c->vector[2] = a->vector[2] - b->vector[2];
                        break;

                    case ProgramOperator.OP_MUL_F:
                        c->_float = a->_float * b->_float;
                        break;

                    case ProgramOperator.OP_MUL_V:
                        c->_float = a->vector[0] * b->vector[0]
                                + a->vector[1] * b->vector[1]
                                + a->vector[2] * b->vector[2];
                        break;

                    case ProgramOperator.OP_MUL_FV:
                        c->vector[0] = a->_float * b->vector[0];
                        c->vector[1] = a->_float * b->vector[1];
                        c->vector[2] = a->_float * b->vector[2];
                        break;

                    case ProgramOperator.OP_MUL_VF:
                        c->vector[0] = b->_float * a->vector[0];
                        c->vector[1] = b->_float * a->vector[1];
                        c->vector[2] = b->_float * a->vector[2];
                        break;

                    case ProgramOperator.OP_DIV_F:
                        c->_float = a->_float / b->_float;
                        break;

                    case ProgramOperator.OP_BITAND:
                        c->_float = ( Int32 ) a->_float & ( Int32 ) b->_float;
                        break;

                    case ProgramOperator.OP_BITOR:
                        c->_float = ( Int32 ) a->_float | ( Int32 ) b->_float;
                        break;

                    case ProgramOperator.OP_GE:
                        c->_float = ( a->_float >= b->_float ) ? 1 : 0;
                        break;

                    case ProgramOperator.OP_LE:
                        c->_float = ( a->_float <= b->_float ) ? 1 : 0;
                        break;

                    case ProgramOperator.OP_GT:
                        c->_float = ( a->_float > b->_float ) ? 1 : 0;
                        break;

                    case ProgramOperator.OP_LT:
                        c->_float = ( a->_float < b->_float ) ? 1 : 0;
                        break;

                    case ProgramOperator.OP_AND:
                        c->_float = ( a->_float != 0 && b->_float != 0 ) ? 1 : 0;
                        break;

                    case ProgramOperator.OP_OR:
                        c->_float = ( a->_float != 0 || b->_float != 0 ) ? 1 : 0;
                        break;

                    case ProgramOperator.OP_NOT_F:
                        c->_float = ( a->_float != 0 ) ? 0 : 1;
                        break;

                    case ProgramOperator.OP_NOT_V:
                        c->_float = ( a->vector[0] == 0 && a->vector[1] == 0 && a->vector[2] == 0 ) ? 1 : 0;
                        break;

                    case ProgramOperator.OP_NOT_S:
                        c->_float = ( a->_string == 0 || String.IsNullOrEmpty( _state.GetString( a->_string ) ) ) ? 1 : 0;
                        break;

                    case ProgramOperator.OP_NOT_FNC:
                        c->_float = ( a->function == 0 ) ? 1 : 0;
                        break;

                    case ProgramOperator.OP_NOT_ENT:
                        c->_float = ( _serverState.ProgToEdict( a->edict ) == _serverState.Data.edicts[0] ) ? 1 : 0;
                        break;

                    case ProgramOperator.OP_EQ_F:
                        c->_float = ( a->_float == b->_float ) ? 1 : 0;
                        break;

                    case ProgramOperator.OP_EQ_V:
                        c->_float = ( ( a->vector[0] == b->vector[0] ) &&
                            ( a->vector[1] == b->vector[1] ) &&
                            ( a->vector[2] == b->vector[2] ) ) ? 1 : 0;
                        break;

                    case ProgramOperator.OP_EQ_S:
                        c->_float = ( _state.GetString( a->_string ) == _state.GetString( b->_string ) ) ? 1 : 0; //!strcmp(pr_strings + a->_string, pr_strings + b->_string);
                        break;

                    case ProgramOperator.OP_EQ_E:
                        c->_float = ( a->_int == b->_int ) ? 1 : 0;
                        break;

                    case ProgramOperator.OP_EQ_FNC:
                        c->_float = ( a->function == b->function ) ? 1 : 0;
                        break;

                    case ProgramOperator.OP_NE_F:
                        c->_float = ( a->_float != b->_float ) ? 1 : 0;
                        break;

                    case ProgramOperator.OP_NE_V:
                        c->_float = ( ( a->vector[0] != b->vector[0] ) ||
                            ( a->vector[1] != b->vector[1] ) || ( a->vector[2] != b->vector[2] ) ) ? 1 : 0;
                        break;

                    case ProgramOperator.OP_NE_S:
                        c->_float = ( _state.GetString( a->_string ) != _state.GetString( b->_string ) ) ? 1 : 0; //strcmp(pr_strings + a->_string, pr_strings + b->_string);
                        break;

                    case ProgramOperator.OP_NE_E:
                        c->_float = ( a->_int != b->_int ) ? 1 : 0;
                        break;

                    case ProgramOperator.OP_NE_FNC:
                        c->_float = ( a->function != b->function ) ? 1 : 0;
                        break;

                    case ProgramOperator.OP_STORE_F:
                    case ProgramOperator.OP_STORE_ENT:
                    case ProgramOperator.OP_STORE_FLD:		// integers
                    case ProgramOperator.OP_STORE_S:
                    case ProgramOperator.OP_STORE_FNC:		// pointers
                        b->_int = a->_int;
                        break;

                    case ProgramOperator.OP_STORE_V:
                        b->vector[0] = a->vector[0];
                        b->vector[1] = a->vector[1];
                        b->vector[2] = a->vector[2];
                        break;

                    case ProgramOperator.OP_STOREP_F:
                    case ProgramOperator.OP_STOREP_ENT:
                    case ProgramOperator.OP_STOREP_FLD:		// integers
                    case ProgramOperator.OP_STOREP_S:
                    case ProgramOperator.OP_STOREP_FNC:		// pointers
                        ed = EdictFromAddr( b->_int, out ofs );
                        ed.StoreInt( ofs, a );
                        break;

                    case ProgramOperator.OP_STOREP_V:
                        ed = EdictFromAddr( b->_int, out ofs );
                        ed.StoreVector( ofs, a );
                        break;

                    case ProgramOperator.OP_ADDRESS:
                        ed = _serverState.ProgToEdict( a->edict );
                        if ( ed == _serverState.Data.edicts[0] && _serverState.IsActive )
                            _programErrors.RunError( "assignment to world entity" );
                        c->_int = MakeAddr( a->edict, b->_int );
                        break;

                    case ProgramOperator.OP_LOAD_F:
                    case ProgramOperator.OP_LOAD_FLD:
                    case ProgramOperator.OP_LOAD_ENT:
                    case ProgramOperator.OP_LOAD_S:
                    case ProgramOperator.OP_LOAD_FNC:
                        ed = _serverState.ProgToEdict( a->edict );
                        ed.LoadInt( b->_int, c );
                        break;

                    case ProgramOperator.OP_LOAD_V:
                        ed = _serverState.ProgToEdict( a->edict );
                        ed.LoadVector( b->_int, c );
                        break;

                    case ProgramOperator.OP_IFNOT:
                        if ( a->_int == 0 )
                            s += _state.Statements[s].b - 1;	// offset the s++
                        break;

                    case ProgramOperator.OP_IF:
                        if ( a->_int != 0 )
                            s += _state.Statements[s].b - 1;	// offset the s++
                        break;

                    case ProgramOperator.OP_GOTO:
                        s += _state.Statements[s].a - 1;	// offset the s++
                        break;

                    case ProgramOperator.OP_CALL0:
                    case ProgramOperator.OP_CALL1:
                    case ProgramOperator.OP_CALL2:
                    case ProgramOperator.OP_CALL3:
                    case ProgramOperator.OP_CALL4:
                    case ProgramOperator.OP_CALL5:
                    case ProgramOperator.OP_CALL6:
                    case ProgramOperator.OP_CALL7:
                    case ProgramOperator.OP_CALL8:
                        _state.ArgC = _state.Statements[s].op - ( Int32 ) ProgramOperator.OP_CALL0;

                        if ( a->function == 0 )
                            _programErrors.RunError( "NULL function" );

                        var newf = _state.Functions[a->function];

                        if ( newf.first_statement < 0 )
                        {
                            // negative statements are built in functions
                            var i = -newf.first_statement;

                            if ( i >= _state.BuiltInCount )
                                _programErrors.RunError( "Bad builtin call number" );

                            _state.OnExecuteBuiltIn?.Invoke( i );
                            break;
                        }

                        s = EnterFunction( newf );
                        break;

                    case ProgramOperator.OP_DONE:
                    case ProgramOperator.OP_RETURN:
                        var ptr = ( Single* ) _state.GlobalStructAddr;
                        Int32 sta = _state.Statements[s].a;
                        ptr[ProgramOperatorDef.OFS_RETURN + 0] = *( Single* ) _state.Get( sta );
                        ptr[ProgramOperatorDef.OFS_RETURN + 1] = *( Single* ) _state.Get( sta + 1 );
                        ptr[ProgramOperatorDef.OFS_RETURN + 2] = *( Single* ) _state.Get( sta + 2 );

                        s = LeaveFunction( );
                        if ( _state.Depth == exitdepth )
                            return;		// all done
                        break;

                    case ProgramOperator.OP_STATE:
                        ed = _serverState.ProgToEdict( _state.GlobalStruct.self );
#if FPS_20
                        ed->v.nextthink = pr_global_struct->time + 0.05;
#else
                        ed.v.nextthink = _state.GlobalStruct.time + 0.1f;
#endif
                        if ( a->_float != ed.v.frame )
                        {
                            ed.v.frame = a->_float;
                        }
                        ed.v.think = b->function;
                        break;

                    default:
                        _programErrors.RunError( "Bad opcode %i", _state.Statements[s].op );
                        break;
                }
            }
        }


        public MemoryEdict EdictFromAddr( Int32 addr, out Int32 ofs )
        {
            var prog = ( addr >> 16 ) & 0xFFFF;
            ofs = addr & 0xFFFF;
            return _serverState.ProgToEdict( prog );
        }

        public void Initialise( )
        {
            _commands.Add( "profile", Profile_f );
        }

        // PR_Profile_f
        private void Profile_f( CommandMessage msg )
        {
            if ( _state.Functions == null )
                return;

            ProgramFunction best;
            var num = 0;
            do
            {
                var max = 0;
                best = null;
                for ( var i = 0; i < _state.Functions.Length; i++ )
                {
                    var f = _state.Functions[i];
                    if ( f.profile > max )
                    {
                        max = f.profile;
                        best = f;
                    }
                }
                if ( best != null )
                {
                    if ( num < 10 )
                        _logger.Print( "{0,7} {1}\n", best.profile, _state.GetString( best.s_name ) );
                    num++;
                    best.profile = 0;
                }
            } while ( best != null );
        }

        /// <summary>
        /// PR_EnterFunction
        /// Returns the new program statement counter
        /// </summary>
        private unsafe Int32 EnterFunction( ProgramFunction f )
        {
            _state.Stack[_state.Depth].s = _state.xStatement;
            _state.Stack[_state.Depth].f = _state.xFunction;
            _state.Depth++;
            if ( _state.Depth >= ProgramsState.MAX_STACK_DEPTH )
                _programErrors.RunError( "stack overflow" );

            // save off any locals that the new function steps on
            var c = f.locals;
            if ( _state.LocalStackUsed + c > ProgramsState.LOCALSTACK_SIZE )
                _programErrors.RunError( "PR_ExecuteProgram: locals stack overflow\n" );

            for ( var i = 0; i < c; i++ )
                _state.LocalStack[_state.LocalStackUsed + i] = *( Int32* ) _state.Get( f.parm_start + i );

            _state.LocalStackUsed += c;

            // copy parameters
            var o = f.parm_start;

            for ( var i = 0; i < f.numparms; i++ )
            {
                for ( var j = 0; j < f.parm_size[i]; j++ )
                {
                    _state.Set( o, *( Int32* ) _state.Get( ProgramOperatorDef.OFS_PARM0 + i * 3 + j ) );
                    o++;
                }
            }

            _state.xFunction = f;
            return f.first_statement - 1;	// offset the s++
        }


        /// <summary>
        /// PR_LeaveFunction
        /// </summary>
        private Int32 LeaveFunction( )
        {
            if ( _state.Depth <= 0 )
                Utilities.Error( "prog stack underflow" );

            // restore locals from the stack
            var c = _state.xFunction.locals;
            _state.LocalStackUsed -= c;

            if ( _state.LocalStackUsed < 0 )
                _programErrors.RunError( "PR_ExecuteProgram: locals stack underflow\n" );

            for ( var i = 0; i < c; i++ )
            {
                _state.Set( _state.xFunction.parm_start + i, _state.LocalStack[_state.LocalStackUsed + i] );
                //((int*)pr_globals)[pr_xfunction->parm_start + i] = localstack[localstack_used + i];
            }

            // up stack
            _state.Depth--;
            _state.xFunction = _state.Stack[_state.Depth].f;

            return _state.Stack[_state.Depth].s;
        }

        private Int32 MakeAddr( Int32 prog, Int32 offset )
        {
            return ( ( prog & 0xFFFF ) << 16 ) + ( offset & 0xFFFF );
        }
    }
}
