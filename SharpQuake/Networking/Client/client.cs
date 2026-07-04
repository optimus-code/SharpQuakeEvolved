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
using SharpQuake.Factories.Rendering;
using SharpQuake.Framework;
using SharpQuake.Framework.Factories.IO;
using SharpQuake.Framework.Logging;
using SharpQuake.Framework.Mathematics;
using SharpQuake.Game.Client;
using SharpQuake.Networking.Client;
using SharpQuake.Networking.Server;
using SharpQuake.Rendering;
using SharpQuake.Rendering.Cameras;
using SharpQuake.Sys;
using SharpQuake.Sys.Handlers;
using System;

// client.h

namespace SharpQuake
{
	public partial class client
	{
		public Single ForwardSpeed
		{
			get
			{
				return Cvars.ForwardSpeed.Get<Single>();
			}
		}

		public Boolean LookSpring
		{
			get
			{
				return Cvars.LookSpring.Get<Boolean>();
			}
		}

		public Boolean LookStrafe
		{
			get
			{
				return Cvars.LookStrafe.Get<Boolean>();
			}
		}

		public Single Sensitivity
		{
			get
			{
				return Cvars.Sensitivity.Get<Single>();
			}
		}

		public Single MSide
		{
			get
			{
				return Cvars.MSide.Get<Single>();
			}
		}

		public Single MYaw
		{
			get
			{
				return Cvars.MYaw.Get<Single>();
			}
		}

		public Single MPitch
		{
			get
			{
				return Cvars.MPitch.Get<Single>();
			}
		}

		public Single MForward
		{
			get
			{
				return Cvars.MForward.Get<Single>();
			}
		}

		public String Name
		{
			get
			{
				return Cvars.Name.Get<String>();
			}
		}

		public Single Color
		{
			get
			{
				return Cvars.Color.Get<Single>();
			}
		}

		private readonly IEngine _engine;
		private readonly IConsoleLogger _logger;
		private readonly ICache _cache;
		private readonly IKeyboardInput _keyboard;
		private readonly IMouseInput _mouse;
		private readonly ClientVariableFactory _cvars;
		private readonly CommandFactory _commands;
        private readonly ModelFactory _models;
        private readonly render _renderer;
		private readonly Scr _screen;
		private readonly View _view;
        private readonly ChaseView _chaseView;
        private readonly snd _sound;
		private readonly Network _network;
		private readonly client_input _clientInput;
		private readonly ClientState _state;
        private readonly ServerState _serverState;
        private readonly MemoryHandler _memoryHandler;
		private readonly VideoState _videoState;
        public client( IEngine engine, IConsoleLogger logger, ClientVariableFactory cvars,
			CommandFactory commands, ModelFactory models, ICache cache, IKeyboardInput keyboard, 
			IMouseInput mouse, render renderer, Scr screen, View view, ChaseView chaseView, snd sound,
			Network network, ClientState state, ServerState serverState, MemoryHandler memoryHandler,
			VideoState videoState )
		{
			_engine = engine;
			_logger = logger;
			_cache = cache;
			_keyboard = keyboard;
			_mouse = mouse;
			_cvars = cvars;
			_commands = commands;
			_models = models;
			_renderer = renderer;
			_screen = screen;
			_sound = sound;
			_network = network;
			_view = view;
			_view.OnApplyDriftPitch += DriftPitch;
            _chaseView = chaseView;
			_state = state;
            _serverState = serverState;
            _memoryHandler = memoryHandler;
			_videoState = videoState;

			_sound.ClientState = _state.Data; // Pass the reference of client state to sound to break circular dependency of snd <-> client.

			_mouse.OnMouseMove += MouseMove; // Assign mouse move handler

			_clientInput = new client_input( _logger, this, _commands, _view );
		}

		/// <summary>
		/// IN_MouseMove
		/// </summary>
		/// <remarks>
		/// (Moved here and created a new event in the mouse class.)
		/// </remarks>
		/// <param name="x"></param>
		/// <param name="y"></param>
		private void MouseMove( usercmd_t cmd )
		{			
			if ( _clientInput.StrafeBtn.IsDown || ( LookStrafe && _clientInput.MLookBtn.IsDown ) )
				cmd.sidemove += MSide * _mouse.Mouse.X;
			else
				_state.Data.viewangles.Y -= MYaw * _mouse.Mouse.X;

			_view.StopPitchDrift( );

			_state.Data.viewangles.X += MPitch * _mouse.Mouse.Y;

			// modernized to always use mouse look
			_state.Data.viewangles.X = MathHelper.Clamp( _state.Data.viewangles.X, -70, 80 );
		}


		// CL_SendMove
		public void SendMove( ref usercmd_t cmd )
		{
			_state.Data.cmd = cmd; // _state.Data.cmd = *cmd - struct copying!!!

			var msg = new MessageWriter( 128 );

			//
			// send the movement message
			//
			msg.WriteByte( ProtocolDef.clc_move );

			msg.WriteFloat( ( Single ) _state.Data.mtime[0] );   // so server can get ping times

			msg.WriteAngle( _state.Data.viewangles.X );
			msg.WriteAngle( _state.Data.viewangles.Y );
			msg.WriteAngle( _state.Data.viewangles.Z );

			msg.WriteShort( ( Int16 ) cmd.forwardmove );
			msg.WriteShort( ( Int16 ) cmd.sidemove );
			msg.WriteShort( ( Int16 ) cmd.upmove );

			//
			// send button bits
			//
			var bits = 0;

			if ( ( _clientInput.AttackBtn.state & 3 ) != 0 )
				bits |= 1;
			_clientInput.AttackBtn.state &= ~2;

			if ( ( _clientInput.JumpBtn.state & 3 ) != 0 )
				bits |= 2;
			_clientInput.JumpBtn.state &= ~2;

			msg.WriteByte( bits );

			msg.WriteByte( _clientInput.Impulse );
			_clientInput.Impulse = 0;

			//
			// deliver the message
			//
			if ( _state.StaticData.demoplayback )
				return;

			//
			// allways dump the first two message, because it may contain leftover inputs
			// from the last level
			//
			if ( ++_state.Data.movemessages <= 2 )
				return;

			if ( _network.SendUnreliableMessage( _state.StaticData.netcon, msg ) == -1 )
			{
				_logger.Print( "CL_SendMove: lost server connection\n" );
				Disconnect( );
			}
		}

		// CL_InitInput
		private void InitInput( )
		{
			_clientInput.Init( );
		}

		/// <summary>
		/// CL_BaseMove
		/// Send the intended movement message to the server
		/// </summary>
		private void BaseMove( ref usercmd_t cmd )
		{
			if ( _state.StaticData.signon != ClientDef.SIGNONS )
				return;

			AdjustAngles( );

			cmd.Clear( );

			if ( _clientInput.StrafeBtn.IsDown )
			{
				cmd.sidemove += Cvars.SideSpeed.Get<Single>( ) * KeyState( ref _clientInput.RightBtn );
				cmd.sidemove -= Cvars.SideSpeed.Get<Single>( ) * KeyState( ref _clientInput.LeftBtn );
			}

			cmd.sidemove += Cvars.SideSpeed.Get<Single>( ) * KeyState( ref _clientInput.MoveRightBtn );
			cmd.sidemove -= Cvars.SideSpeed.Get<Single>( ) * KeyState( ref _clientInput.MoveLeftBtn );

			var upBtn = KeyState( ref _clientInput.UpBtn );
			if ( upBtn > 0 )
				Console.WriteLine( "asd" );
			cmd.upmove += Cvars.UpSpeed.Get<Single>( ) * KeyState( ref _clientInput.UpBtn );
			cmd.upmove -= Cvars.UpSpeed.Get<Single>( ) * KeyState( ref _clientInput.DownBtn );

			if ( !_clientInput.KLookBtn.IsDown )
			{
				cmd.forwardmove += Cvars.ForwardSpeed.Get<Single>( ) * KeyState( ref _clientInput.ForwardBtn );
				cmd.forwardmove -= Cvars.BackSpeed.Get<Single>( ) * KeyState( ref _clientInput.BackBtn );
			}

			//
			// adjust for speed key
			//
			if ( _clientInput.SpeedBtn.IsDown )
			{
				cmd.forwardmove *= Cvars.MoveSpeedKey.Get<Single>( );
				cmd.sidemove *= Cvars.MoveSpeedKey.Get<Single>( );
				cmd.upmove *= Cvars.MoveSpeedKey.Get<Single>( );
			}
		}

		// CL_AdjustAngles
		//
		// Moves the local angle positions
		private void AdjustAngles( )
		{
			var speed = ( Single ) Time.Delta;

			if ( _clientInput.SpeedBtn.IsDown )
				speed *= Cvars.AngleSpeedKey.Get<Single>( );

			if ( !_clientInput.StrafeBtn.IsDown )
			{
				_state.Data.viewangles.Y -= speed * Cvars.YawSpeed.Get<Single>( ) * KeyState( ref _clientInput.RightBtn );
				_state.Data.viewangles.Y += speed * Cvars.YawSpeed.Get<Single>( ) * KeyState( ref _clientInput.LeftBtn );
				_state.Data.viewangles.Y = MathLib.AngleMod( _state.Data.viewangles.Y );
			}

			if ( _clientInput.KLookBtn.IsDown )
			{
				_view.StopPitchDrift( );
				_state.Data.viewangles.X -= speed * Cvars.PitchSpeed.Get<Single>( ) * KeyState( ref _clientInput.ForwardBtn );
				_state.Data.viewangles.X += speed * Cvars.PitchSpeed.Get<Single>( ) * KeyState( ref _clientInput.BackBtn );
			}

			var up = KeyState( ref _clientInput.LookUpBtn );
			var down = KeyState( ref _clientInput.LookDownBtn );

			_state.Data.viewangles.X -= speed * Cvars.PitchSpeed.Get<Single>( ) * up;
			_state.Data.viewangles.X += speed * Cvars.PitchSpeed.Get<Single>( ) * down;

			if ( up != 0 || down != 0 )
				_view.StopPitchDrift( );

			if ( _state.Data.viewangles.X > 80 )
				_state.Data.viewangles.X = 80;
			if ( _state.Data.viewangles.X < -70 )
				_state.Data.viewangles.X = -70;

			if ( _state.Data.viewangles.Z > 50 )
				_state.Data.viewangles.Z = 50;
			if ( _state.Data.viewangles.Z < -50 )
				_state.Data.viewangles.Z = -50;
		}

		// CL_KeyState
		//
		// Returns 0.25 if a key was pressed and released during the frame,
		// 0.5 if it was pressed and held
		// 0 if held then released, and
		// 1.0 if held for the entire time
		private Single KeyState( ref kbutton_t key )
		{
			var impulsedown = ( key.state & 2 ) != 0;
			var impulseup = ( key.state & 4 ) != 0;
			var down = key.IsDown;// ->state & 1;
			Single val = 0;

			if ( impulsedown && !impulseup )
				if ( down )
					val = 0.5f; // pressed and held this frame
				else
					val = 0;    //	I_Error ();
			if ( impulseup && !impulsedown )
				if ( down )
					val = 0;    //	I_Error ();
				else
					val = 0;    // released this frame
			if ( !impulsedown && !impulseup )
				if ( down )
					val = 1.0f; // held the entire frame
				else
					val = 0;    // up the entire frame
			if ( impulsedown && impulseup )
				if ( down )
					val = 0.75f;    // released and re-pressed this frame
				else
					val = 0.25f;    // pressed and released this frame

			key.state &= 1;     // clear impulses

			return val;
		}

        private void DriftPitch( )
        {
            var cl = _state.Data;
            if ( _engine.NoClipAngleHack || !cl.onground || _state.StaticData.demoplayback )
            {
                cl.driftmove = 0;
                cl.pitchvel = 0;
                return;
            }

            // don't count small mouse motion
            if ( cl.nodrift )
            {
                if ( Math.Abs( cl.cmd.forwardmove ) < ForwardSpeed )
                    cl.driftmove = 0;
                else
                    cl.driftmove += ( Single ) Time.Delta;

                if ( cl.driftmove > Cvars.CenterMove.Get<Single>( ) )
                {
                    _view.StartPitchDrift( null );
                }
                return;
            }

            var delta = cl.idealpitch - cl.viewangles.X;
            if ( delta == 0 )
            {
                cl.pitchvel = 0;
                return;
            }

            var move = ( Single ) Time.Delta * cl.pitchvel;
            cl.pitchvel += ( Single ) Time.Delta * Cvars.CenterSpeed.Get<Single>( );

            if ( delta > 0 )
            {
                if ( move > delta )
                {
                    cl.pitchvel = 0;
                    move = delta;
                }
                cl.viewangles.X += move;
            }
            else if ( delta < 0 )
            {
                if ( move > -delta )
                {
                    cl.pitchvel = 0;
                    move = -delta;
                }
                cl.viewangles.X -= move;
            }
        }
    }
}