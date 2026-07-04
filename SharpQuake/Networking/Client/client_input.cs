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
using SharpQuake.Framework;
using SharpQuake.Framework.Factories.IO;
using SharpQuake.Framework.IO;
using SharpQuake.Framework.Logging;
using SharpQuake.Game.Client;

// cl_input.c

namespace SharpQuake
{
    public class client_input
    {
        // kbutton_t in_xxx
        public kbutton_t MLookBtn;

        public kbutton_t KLookBtn;
        public kbutton_t LeftBtn;
        public kbutton_t RightBtn;
        public kbutton_t ForwardBtn;
        public kbutton_t BackBtn;
        public kbutton_t LookUpBtn;
        public kbutton_t LookDownBtn;
        public kbutton_t MoveLeftBtn;
        public kbutton_t MoveRightBtn;
        public kbutton_t StrafeBtn;
        public kbutton_t SpeedBtn;
        public kbutton_t UseBtn;
        public kbutton_t JumpBtn;
        public kbutton_t AttackBtn;
        public kbutton_t UpBtn;
        public kbutton_t DownBtn;

        public Int32 Impulse;

        private readonly IConsoleLogger _logger;
        private readonly client _client;
        private readonly CommandFactory _commands;
        private readonly View _view;

        public client_input( IConsoleLogger logger, client client, CommandFactory commands, View view )
        {
            _logger = logger;
            _client = client;
            _commands = commands;
            _view = view;
        }

        public void Init( )
        {
            _commands.Add( "+moveup", UpDown );
            _commands.Add( "-moveup", UpUp );
            _commands.Add( "+movedown", DownDown );
            _commands.Add( "-movedown", DownUp );
            _commands.Add( "+left", LeftDown );
            _commands.Add( "-left", LeftUp );
            _commands.Add( "+right", RightDown );
            _commands.Add( "-right", RightUp );
            _commands.Add( "+forward", ForwardDown );
            _commands.Add( "-forward", ForwardUp );
            _commands.Add( "+back", BackDown );
            _commands.Add( "-back", BackUp );
            _commands.Add( "+lookup", LookupDown );
            _commands.Add( "-lookup", LookupUp );
            _commands.Add( "+lookdown", LookdownDown );
            _commands.Add( "-lookdown", LookdownUp );
            _commands.Add( "+strafe", StrafeDown );
            _commands.Add( "-strafe", StrafeUp );
            _commands.Add( "+moveleft", MoveleftDown );
            _commands.Add( "-moveleft", MoveleftUp );
            _commands.Add( "+moveright", MoverightDown );
            _commands.Add( "-moveright", MoverightUp );
            _commands.Add( "+speed", SpeedDown );
            _commands.Add( "-speed", SpeedUp );
            _commands.Add( "+attack", AttackDown );
            _commands.Add( "-attack", AttackUp );
            _commands.Add( "+use", UseDown );
            _commands.Add( "-use", UseUp );
            _commands.Add( "+jump", JumpDown );
            _commands.Add( "-jump", JumpUp );
            _commands.Add( "impulse", ImpulseCmd );
            _commands.Add( "+klook", KLookDown );
            _commands.Add( "-klook", KLookUp );
            _commands.Add( "+mlook", MLookDown );
            _commands.Add( "-mlook", MLookUp );
        }

        private void KeyDown( CommandMessage msg, ref kbutton_t b )
        {
            Int32 k;
            if ( msg.Parameters?.Length > 0 && !String.IsNullOrEmpty( msg.Parameters[0] ) )
                k = Int32.Parse( msg.Parameters[0] );
            else
                k = -1;	// typed manually at the console for continuous down

            if ( k == b.down0 || k == b.down1 )
                return;		// repeating key

            if ( b.down0 == 0 )
                b.down0 = k;
            else if ( b.down1 == 0 )
                b.down1 = k;
            else
            {
                _logger.Print( "Three keys down for a button!\n" );
                return;
            }

            if ( ( b.state & 1 ) != 0 )
                return;	// still down
            b.state |= 1 + 2; // down + impulse down
        }

        private void KeyUp( CommandMessage msg, ref kbutton_t b )
        {
            Int32 k;
            if ( msg.Parameters?.Length > 0 && !String.IsNullOrEmpty( msg.Parameters[0] ) )
                k = Int32.Parse( msg.Parameters[0] );
            else
            {
                // typed manually at the console, assume for unsticking, so clear all
                b.down0 = b.down1 = 0;
                b.state = 4;	// impulse up
                return;
            }

            if ( b.down0 == k )
                b.down0 = 0;
            else if ( b.down1 == k )
                b.down1 = 0;
            else
                return;	// key up without coresponding down (menu pass through)

            if ( b.down0 != 0 || b.down1 != 0 )
                return;	// some other key is still holding it down

            if ( ( b.state & 1 ) == 0 )
                return;		// still up (this should not happen)
            b.state &= ~1;		// now up
            b.state |= 4; 		// impulse up
        }

        private void KLookDown( CommandMessage msg )
        {
            KeyDown( msg, ref KLookBtn );
        }

        private void KLookUp( CommandMessage msg )
        {
            KeyUp( msg, ref KLookBtn );
        }

        private void MLookDown( CommandMessage msg )
        {
            KeyDown( msg, ref MLookBtn );
        }

        private void MLookUp( CommandMessage msg )
        {
            KeyUp( msg, ref MLookBtn );

            if ( ( MLookBtn.state & 1 ) == 0 && _client.LookSpring )
                _view.StartPitchDrift( null );
        }

        private void UpDown( CommandMessage msg )
        {
            KeyDown( msg, ref UpBtn );
        }

        private void UpUp( CommandMessage msg )
        {
            KeyUp( msg, ref UpBtn );
        }

        private void DownDown( CommandMessage msg )
        {
            KeyDown( msg, ref DownBtn );
        }

        private void DownUp( CommandMessage msg )
        {
            KeyUp( msg, ref DownBtn );
        }

        private void LeftDown( CommandMessage msg )
        {
            KeyDown( msg, ref LeftBtn );
        }

        private void LeftUp( CommandMessage msg )
        {
            KeyUp( msg, ref LeftBtn );
        }

        private void RightDown( CommandMessage msg )
        {
            KeyDown( msg, ref RightBtn );
        }

        private void RightUp( CommandMessage msg )
        {
            KeyUp( msg, ref RightBtn );
        }

        private void ForwardDown( CommandMessage msg )
        {
            KeyDown( msg, ref ForwardBtn );
        }

        private void ForwardUp( CommandMessage msg )
        {
            KeyUp( msg, ref ForwardBtn );
        }

        private void BackDown( CommandMessage msg )
        {
            KeyDown( msg, ref BackBtn );
        }

        private void BackUp( CommandMessage msg )
        {
            KeyUp( msg, ref BackBtn );
        }

        private void LookupDown( CommandMessage msg )
        {
            KeyDown( msg, ref LookUpBtn );
        }

        private void LookupUp( CommandMessage msg )
        {
            KeyUp( msg, ref LookUpBtn );
        }

        private void LookdownDown( CommandMessage msg )
        {
            KeyDown( msg, ref LookDownBtn );
        }

        private void LookdownUp( CommandMessage msg )
        {
            KeyUp( msg, ref LookDownBtn );
        }

        private void MoveleftDown( CommandMessage msg )
        {
            KeyDown( msg, ref MoveLeftBtn );
        }

        private void MoveleftUp( CommandMessage msg )
        {
            KeyUp( msg, ref MoveLeftBtn );
        }

        private void MoverightDown( CommandMessage msg )
        {
            KeyDown( msg, ref MoveRightBtn );
        }

        private void MoverightUp( CommandMessage msg )
        {
            KeyUp( msg, ref MoveRightBtn );
        }

        private void SpeedDown( CommandMessage msg )
        {
            KeyDown( msg, ref SpeedBtn );
        }

        private void SpeedUp( CommandMessage msg )
        {
            KeyUp( msg, ref SpeedBtn );
        }

        private void StrafeDown( CommandMessage msg )
        {
            KeyDown( msg, ref StrafeBtn );
        }

        private void StrafeUp( CommandMessage msg )
        {
            KeyUp( msg, ref StrafeBtn );
        }

        private void AttackDown( CommandMessage msg )
        {
            KeyDown( msg, ref AttackBtn );
        }

        private void AttackUp( CommandMessage msg )
        {
            KeyUp( msg, ref AttackBtn );
        }

        private void UseDown( CommandMessage msg )
        {
            KeyDown( msg, ref UseBtn );
        }

        private void UseUp( CommandMessage msg )
        {
            KeyUp( msg, ref UseBtn );
        }

        private void JumpDown( CommandMessage msg )
        {
            KeyDown( msg, ref JumpBtn );
        }

        private void JumpUp( CommandMessage msg )
        {
            KeyUp( msg, ref JumpBtn );
        }

        private void ImpulseCmd( CommandMessage msg )
        {
            Impulse = MathLib.atoi( msg.Parameters[0] );
        }
    }
}
