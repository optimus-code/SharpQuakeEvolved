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
using SharpQuake.Framework.Mathematics;
using SharpQuake.Framework;
using SharpQuake.Framework.IO.BSP;
using SharpQuake.Framework.IO;
using SharpQuake.Game.Client;
using SharpQuake.Framework.Definitions;
using SharpQuake.Rendering.Cameras;
using SharpQuake.Sys;
using SharpQuake.Framework.Factories.IO;
using SharpQuake.Rendering;
using SharpQuake.Networking.Client;

// view.h
// view.c -- player eye positioning

// The view is allowed to move slightly from it's true position for bobbing,
// but if it exceeds 8 pixels linear distance (spherical, not box), the list of
// entities sent from the server may not include everything in the pvs, especially
// when crossing a water boudnary.

namespace SharpQuake
{
	/// <summary>
	/// V_functions
	/// </summary>
	public class View
	{
		public Single Crosshair
		{
			get
			{
				return Cvars.Crosshair.Get<Single>();
			}
		}

		public Single Gamma
		{
			get
			{
				return Cvars.Gamma.Get<Single>();
			}
		}

		public Int32 FrameCount
		{
			get;
			set;
		}

		/// <summary>
		/// Used to bypass rendering
		/// </summary>
		/// <remarks>
		/// (E.g. If the full screen console is up)
		/// </remarks>
		public Boolean NoRender
        {
			get;
			set;
        }

		public Color4 Blend;
		private static readonly Vector3 SmallOffset = Vector3.One / 32f;

		private Byte[] _GammaTable; // [256];	// palette is sent through this
		private cshift_t _CShift_empty;// = { { 130, 80, 50 }, 0 };
		private cshift_t _CShift_water;// = { { 130, 80, 50 }, 128 };
		private cshift_t _CShift_slime;// = { { 0, 25, 5 }, 150 };
		private cshift_t _CShift_lava;// = { { 255, 80, 0 }, 150 };

		// v_blend[4]		// rgba 0.0 - 1.0
		private Byte[,] _Ramps = new Byte[3, 256]; // ramps[3][256]

		private Single _OldZ = 0; // static oldz  from CalcRefdef()
		private Single _OldYaw = 0; // static oldyaw from CalcGunAngle
		private Single _OldPitch = 0; // static oldpitch from CalcGunAngle
		private Single _OldGammaValue; // static float oldgammavalue from CheckGamma

		public Camera MainCamera
		{
			get;
			private set;
		}

        public ChaseView ChaseView
        {
            get
			{
				return _chaseView;
			}
        }

		public Action OnApplyDriftPitch
		{
			get;
			set;
		}

        private readonly IEngine _engine;
        private readonly IGameRenderer _gameRenderer;
		private readonly ClientVariableFactory _cvars;
		private readonly CommandFactory _commands;
        private readonly Network _network;
		private readonly ChaseView _chaseView;
		private readonly ClientState _clientState;
		private readonly VideoState _videoState;
		private readonly RenderState _renderState;

        public View( IEngine engine, IGameRenderer gameRenderer, CommandFactory commands,
			ClientVariableFactory cvars, ClientState clientState, Network network,
			ChaseView chaseView, VideoState videoState, RenderState renderState )
		{
			_engine = engine;
			_gameRenderer = gameRenderer;
			_commands = commands;
			_cvars = cvars;
			_clientState = clientState;
            _network = network;
			_videoState = videoState;
			_renderState = renderState;

			_GammaTable = new Byte[256];

			_CShift_empty = new cshift_t( new[] { 130, 80, 50 }, 0 );
			_CShift_water = new cshift_t( new[] { 130, 80, 50 }, 128 );
			_CShift_slime = new cshift_t( new[] { 0, 25, 5 }, 150 );
			_CShift_lava = new cshift_t( new[] { 255, 80, 0 }, 150 );

			MainCamera = new Camera( _cvars, _clientState, _renderState );

			_chaseView = chaseView;
        }

		// V_Init
		public void Initialise( )
		{
			InitialiseCommands();
			InitialiseClientVariables();
			MainCamera.Initialise( );
		}

		private void InitialiseCommands()
		{
			_commands.Add( "v_cshift", CShift_f );
			_commands.Add( "bf", BonusFlash_f );
			_commands.Add( "centerview", StartPitchDrift );
		}

		private void InitialiseClientVariables()
		{
			if ( Cvars.LcdX != null ) // Exit if we've already initialised
				return;

			Cvars.LcdX = _cvars.Add( "lcd_x", 0f );
			Cvars.LcdYaw = _cvars.Add( "lcd_yaw", 0f );

			Cvars.ScrOfsX = _cvars.Add( "scr_ofsx", 0f );
			Cvars.ScrOfsY = _cvars.Add( "scr_ofsy", 0f );
			Cvars.ScrOfsZ = _cvars.Add( "scr_ofsz", 0f );				

			Cvars.KickTime = _cvars.Add( "v_kicktime", 0.5f );
			Cvars.KickRoll = _cvars.Add( "v_kickroll", 0.6f );
			Cvars.KickPitch = _cvars.Add( "v_kickpitch", 0.6f );

			Cvars.IYawCycle = _cvars.Add( "v_iyaw_cycle", 2f );
			Cvars.IRollCycle = _cvars.Add( "v_iroll_cycle", 0.5f );
			Cvars.IPitchCycle = _cvars.Add( "v_ipitch_cycle", 1f );
			Cvars.IYawLevel = _cvars.Add( "v_iyaw_level", 0.3f );
			Cvars.IRollLevel = _cvars.Add( "v_iroll_level", 0.1f );
			Cvars.IPitchLevel = _cvars.Add( "v_ipitch_level", 0.3f );

			Cvars.IdleScale = _cvars.Add( "v_idlescale", 0f );

			Cvars.Crosshair = _cvars.Add( "crosshair", 0f, ClientVariableFlags.Archive );
			Cvars.ClCrossX = _cvars.Add( "cl_crossx", 0f );
			Cvars.ClCrossY = _cvars.Add( "cl_crossy", 0f );

			Cvars.glCShiftPercent = _cvars.Add( "gl_cshiftpercent", 100f );

			Cvars.CenterMove = _cvars.Add( "v_centermove", 0.15f );
			Cvars.CenterSpeed = _cvars.Add( "v_centerspeed", 500f );

			BuildGammaTable( 1.0f );    // no gamma yet
			Cvars.Gamma = _cvars.Add( "gamma", 1f, ClientVariableFlags.Archive );			
		}

		/// <summary>
		/// V_RenderView
		/// The player's clipping box goes from (-16 -16 -24) to (16 16 32) from
		/// the entity origin, so any view position inside that will be valid
		/// </summary>
		public void RenderView( )
		{		
			if ( NoRender )
				return;

			// don't allow cheats in multiplayer
			if ( _clientState.Data.maxclients > 1 )
			{
				_cvars.Set( "scr_ofsx", 0f );
				_cvars.Set( "scr_ofsy", 0f );
				_cvars.Set( "scr_ofsz", 0f );
			}

			if ( _clientState.Data.intermission > 0 )
			{
				// intermission / finale rendering
				CalcIntermissionRefDef();
			}
			else if ( !_clientState.Data.paused )
				CalcRefDef();

			_renderState.OnPushDLights?.Invoke( );

            if ( Cvars.LcdX.Get<Single>() != 0 )
			{
				//
				// render two interleaved views
				//
				var vid = _videoState.Data;
				var rdef = _renderState.Data;

				vid.rowbytes <<= 1;
				vid.aspect *= 0.5f;

				rdef.viewangles.Y -= Cvars.LcdYaw.Get<Single>();
				rdef.vieworg -= MainCamera.Right * Cvars.LcdX.Get<Single>();

				_renderState.OnRenderView?.Invoke( );

                // ???????? vid.buffer += vid.rowbytes>>1;

                _renderState.OnPushDLights?.Invoke( );

                rdef.viewangles.Y += Cvars.LcdYaw.Get<Single>() * 2;
				rdef.vieworg += MainCamera.Right * Cvars.LcdX.Get<Single>() * 2;

                _renderState.OnRenderView?.Invoke( );

				// ????????? vid.buffer -= vid.rowbytes>>1;

				rdef.vrect.height <<= 1;

				vid.rowbytes >>= 1;
				vid.aspect *= 2;
			}
			else
            {
                _renderState.OnRenderView?.Invoke( );
            }
		}

		// V_UpdatePalette
		public void UpdatePalette( )
		{
			CalcPowerupCshift();

			var isnew = false;

			var cl = _clientState.Data;
			for ( var i = 0; i < ColourShiftDef.NUM_CSHIFTS; i++ )
			{
				if ( cl.cshifts[i].percent != cl.prev_cshifts[i].percent )
				{
					isnew = true;
					cl.prev_cshifts[i].percent = cl.cshifts[i].percent;
				}
				for ( var j = 0; j < 3; j++ )
					if ( cl.cshifts[i].destcolor[j] != cl.prev_cshifts[i].destcolor[j] )
					{
						isnew = true;
						cl.prev_cshifts[i].destcolor[j] = cl.cshifts[i].destcolor[j];
					}
			}

			// drop the damage value
			cl.cshifts[ColourShiftDef.CSHIFT_DAMAGE].percent -= ( Int32 ) ( Time.Delta * 150 );
			if ( cl.cshifts[ColourShiftDef.CSHIFT_DAMAGE].percent < 0 )
				cl.cshifts[ColourShiftDef.CSHIFT_DAMAGE].percent = 0;

			// drop the bonus value
			cl.cshifts[ColourShiftDef.CSHIFT_BONUS].percent -= ( Int32 ) ( Time.Delta * 100 );
			if ( cl.cshifts[ColourShiftDef.CSHIFT_BONUS].percent < 0 )
				cl.cshifts[ColourShiftDef.CSHIFT_BONUS].percent = 0;

			var force = CheckGamma();
			if ( !isnew && !force )
				return;

			CalcBlend();

			var a = Blend.A;
			var r = 255 * Blend.R * a;
			var g = 255 * Blend.G * a;
			var b = 255 * Blend.B * a;

			a = 1 - a;
			for ( var i = 0; i < 256; i++ )
			{
				var ir = ( Int32 ) ( i * a + r );
				var ig = ( Int32 ) ( i * a + g );
				var ib = ( Int32 ) ( i * a + b );
				if ( ir > 255 )
					ir = 255;
				if ( ig > 255 )
					ig = 255;
				if ( ib > 255 )
					ib = 255;

				_Ramps[0, i] = _GammaTable[ir];
				_Ramps[1, i] = _GammaTable[ig];
				_Ramps[2, i] = _GammaTable[ib];
			}

			var basepal = _gameRenderer.BasePal;
			var offset = 0;
			var newpal = new Byte[768];

			for ( var i = 0; i < 256; i++ )
			{
				Int32 ir = basepal[offset + 0];
				Int32 ig = basepal[offset + 1];
				Int32 ib = basepal[offset + 2];

				newpal[offset + 0] = _Ramps[0, ir];
				newpal[offset + 1] = _Ramps[1, ig];
				newpal[offset + 2] = _Ramps[2, ib];

				offset += 3;
			}

			ShiftPalette( newpal );
		}

		// V_StartPitchDrift
		public void StartPitchDrift( CommandMessage msg )
		{
			var cl = _clientState.Data;
			if ( cl.laststop == cl.time )
			{
				return; // something else is keeping it from drifting
			}
			if ( cl.nodrift || cl.pitchvel == 0 )
			{
				cl.pitchvel = Cvars.CenterSpeed.Get<Single>();
				cl.nodrift = false;
				cl.driftmove = 0;
			}
		}

		// V_StopPitchDrift
		public void StopPitchDrift( )
		{
			var cl = _clientState.Data;
			cl.laststop = cl.time;
			cl.nodrift = true;
			cl.pitchvel = 0;
		}

		/// <summary>
		/// V_CalcBlend
		/// </summary>
		public void CalcBlend( )
		{
			Single r = 0;
			Single g = 0;
			Single b = 0;
			Single a = 0;

			var cshifts = _clientState.Data.cshifts;

			if ( Cvars.glCShiftPercent.Get<Single>() != 0 )
			{
				for ( var j = 0; j < ColourShiftDef.NUM_CSHIFTS; j++ )
				{
					var a2 = ( ( cshifts[j].percent * Cvars.glCShiftPercent.Get<Single>() ) / 100.0f ) / 255.0f;

					if ( a2 == 0 )
						continue;

					a = a + a2 * ( 1 - a );

					a2 = a2 / a;
					r = r * ( 1 - a2 ) + cshifts[j].destcolor[0] * a2;
					g = g * ( 1 - a2 ) + cshifts[j].destcolor[1] * a2;
					b = b * ( 1 - a2 ) + cshifts[j].destcolor[2] * a2;
				}
			}

			Blend.R = r / 255.0f;
			Blend.G = g / 255.0f;
			Blend.B = b / 255.0f;
			Blend.A = a;
			if ( Blend.A > 1 )
				Blend.A = 1;
			if ( Blend.A < 0 )
				Blend.A = 0;
		}

		// V_ParseDamage
		public void ParseDamage( )
		{
			var armor = _network.Reader.ReadByte();
			var blood = _network.Reader.ReadByte();
			var from = _network.Reader.ReadCoords();

			var count = blood * 0.5f + armor * 0.5f;
			if ( count < 10 )
				count = 10;

			var cl = _clientState.Data;
			cl.faceanimtime = ( Single ) cl.time + 0.2f; // put sbar face into pain frame

			cl.cshifts[ColourShiftDef.CSHIFT_DAMAGE].percent += ( Int32 ) ( 3 * count );
			if ( cl.cshifts[ColourShiftDef.CSHIFT_DAMAGE].percent < 0 )
				cl.cshifts[ColourShiftDef.CSHIFT_DAMAGE].percent = 0;
			if ( cl.cshifts[ColourShiftDef.CSHIFT_DAMAGE].percent > 150 )
				cl.cshifts[ColourShiftDef.CSHIFT_DAMAGE].percent = 150;

			if ( armor > blood )
			{
				cl.cshifts[ColourShiftDef.CSHIFT_DAMAGE].destcolor[0] = 200;
				cl.cshifts[ColourShiftDef.CSHIFT_DAMAGE].destcolor[1] = 100;
				cl.cshifts[ColourShiftDef.CSHIFT_DAMAGE].destcolor[2] = 100;
			}
			else if ( armor != 0 )
			{
				cl.cshifts[ColourShiftDef.CSHIFT_DAMAGE].destcolor[0] = 220;
				cl.cshifts[ColourShiftDef.CSHIFT_DAMAGE].destcolor[1] = 50;
				cl.cshifts[ColourShiftDef.CSHIFT_DAMAGE].destcolor[2] = 50;
			}
			else
			{
				cl.cshifts[ColourShiftDef.CSHIFT_DAMAGE].destcolor[0] = 255;
				cl.cshifts[ColourShiftDef.CSHIFT_DAMAGE].destcolor[1] = 0;
				cl.cshifts[ColourShiftDef.CSHIFT_DAMAGE].destcolor[2] = 0;
			}

			//
			// calculate view angle kicks
			//
			var ent = _clientState.Entities[cl.viewentity];

			from -= ent.origin; //  VectorSubtract (from, ent->origin, from);
			MathLib.Normalize( ref from );

			Vector3 forward, right, up;
			MathLib.AngleVectors( ref ent.angles, out forward, out right, out up );

			var side = Vector3.Dot( from, right );

			MainCamera.DmgRoll = count * side * Cvars.KickRoll.Get<Single>();

			side = Vector3.Dot( from, forward );
			MainCamera.DmgPitch = count * side * Cvars.KickPitch.Get<Single>();

			MainCamera.DmgTime = Cvars.KickTime.Get<Single>();
		}

		/// <summary>
		/// V_SetContentsColor
		/// Underwater, lava, etc each has a color shift
		/// </summary>
		public void SetContentsColor( Int32 contents )
		{
			switch ( ( Q1Contents ) contents )
			{
				case Q1Contents.Empty:
				case Q1Contents.Solid:
					_clientState.Data.cshifts[ColourShiftDef.CSHIFT_CONTENTS] = _CShift_empty;
					break;

				case Q1Contents.Lava:
					_clientState.Data.cshifts[ColourShiftDef.CSHIFT_CONTENTS] = _CShift_lava;
					break;

				case Q1Contents.Slime:
					_clientState.Data.cshifts[ColourShiftDef.CSHIFT_CONTENTS] = _CShift_slime;
					break;

				default:
					_clientState.Data.cshifts[ColourShiftDef.CSHIFT_CONTENTS] = _CShift_water;
					break;
			}
		}

		// BuildGammaTable
		private void BuildGammaTable( Single g )
		{
			if ( g == 1.0f )
			{
				for ( var i = 0; i < 256; i++ )
				{
					_GammaTable[i] = ( Byte ) i;
				}
			}
			else
			{
				for ( var i = 0; i < 256; i++ )
				{
					var inf = ( Int32 ) ( 255 * Math.Pow( ( i + 0.5 ) / 255.5, g ) + 0.5 );
					if ( inf < 0 )
						inf = 0;
					if ( inf > 255 )
						inf = 255;
					_GammaTable[i] = ( Byte ) inf;
				}
			}
		}

		// V_cshift_f
		private void CShift_f( CommandMessage msg )
		{
			Int32.TryParse( msg.Parameters[0], out _CShift_empty.destcolor[0] );
			Int32.TryParse( msg.Parameters[1], out _CShift_empty.destcolor[1] );
			Int32.TryParse( msg.Parameters[2], out _CShift_empty.destcolor[2] );
			Int32.TryParse( msg.Parameters[3], out _CShift_empty.percent );
		}

		// V_BonusFlash_f
		//
		// When you run over an item, the server sends this command
		private void BonusFlash_f( CommandMessage msg )
		{
			var cl = _clientState.Data;
			cl.cshifts[ColourShiftDef.CSHIFT_BONUS].destcolor[0] = 215;
			cl.cshifts[ColourShiftDef.CSHIFT_BONUS].destcolor[1] = 186;
			cl.cshifts[ColourShiftDef.CSHIFT_BONUS].destcolor[2] = 69;
			cl.cshifts[ColourShiftDef.CSHIFT_BONUS].percent = 50;
		}

		// V_CalcIntermissionRefdef
		private void CalcIntermissionRefDef( )
		{
			// ent is the player model (visible when out of body)
			var ent = _clientState.ViewEntity;

			// view is the weapon model (only visible from inside body)
			var view = _clientState.ViewEnt;

			var rdef = _renderState.Data;
			rdef.vieworg = ent.origin;
			rdef.viewangles = ent.angles;
			view.model = null;

			// allways idle in intermission
			var sway = MainCamera.GetTransform<SwayCameraTransform>( );
			sway.OverrideScale = 1;
			sway.Apply( );
			sway.OverrideScale = null;
		}

		// V_CalcRefdef
		private void CalcRefDef( )
		{
			DriftPitch();

			// ent is the player model (visible when out of body)
			var ent = _clientState.ViewEntity;

			// view is the weapon model (only visible from inside body)
			var view = _clientState.ViewEnt;

			// transform the view offset by the model's matrix to get the offset from
			// model origin for the view
			ent.angles.Y = _clientState.Data.viewangles.Y; // the model should face the view dir
			ent.angles.X = -_clientState.Data.viewangles.X;    // the model should face the view dir

			var rdef = _renderState.Data;
			var cl = _clientState.Data;

			var bob = MainCamera.GetTransform<BobCameraTransform>( );
			bob.Apply( );

			// never let it sit exactly on a node line, because a water plane can
			// dissapear when viewed with the eye exactly on it.
			// the server protocol only specifies to 1/16 pixel, so add 1/32 in each axis
			rdef.vieworg += SmallOffset;
			rdef.viewangles = cl.viewangles;

			MainCamera.CalculateViewRoll();
			MainCamera.ApplyTransform<SwayCameraTransform>( );

			// offsets
			var angles = ent.angles;
			angles.X = -angles.X; // because entity pitches are actually backward

			Vector3 forward, right, up;
			MathLib.AngleVectors( ref angles, out forward, out right, out up );

			rdef.vieworg += forward * Cvars.ScrOfsX.Get<Single>() + right * Cvars.ScrOfsY.Get<Single>() + up * Cvars.ScrOfsZ.Get<Single>();

			BoundOffsets();

			// set up gun position
			view.angles = cl.viewangles;

			CalcGunAngle();

			view.origin = ent.origin;
			view.origin.Z += cl.viewheight;
			view.origin += forward * bob.CalculatedValue * 0.4f;
			view.origin.Z += bob.CalculatedValue;

			// fudge position around to keep amount of weapon visible
			// roughly equal with different FOV
			var viewSize = Cvars.ViewSize.Get<Single>(); // scr_viewsize

			if ( viewSize == 110 )
				view.origin.Z += 1;
			else if ( viewSize == 100 )
				view.origin.Z += 2;
			else if ( viewSize == 90 )
				view.origin.Z += 1;
			else if ( viewSize == 80 )
				view.origin.Z += 0.5f;

			view.model = cl.model_precache[cl.stats[QStatsDef.STAT_WEAPON]];
			view.frame = cl.stats[QStatsDef.STAT_WEAPONFRAME];
			view.colormap = _videoState.Data.colormap;

			// set up the refresh position
			rdef.viewangles += cl.punchangle;

			// smooth out stair step ups
			if ( cl.onground && ent.origin.Z - _OldZ > 0 )
			{
				var steptime = ( Single ) ( cl.time - cl.oldtime );
				if ( steptime < 0 )
					steptime = 0;

				_OldZ += steptime * 80;
				if ( _OldZ > ent.origin.Z )
					_OldZ = ent.origin.Z;
				if ( ent.origin.Z - _OldZ > 12 )
					_OldZ = ent.origin.Z - 12;
				rdef.vieworg.Z += _OldZ - ent.origin.Z;
				view.origin.Z += _OldZ - ent.origin.Z;
			}
			else
				_OldZ = ent.origin.Z;

			if ( _chaseView.IsActive )
				_chaseView.Update();
		}

		// V_DriftPitch
		//
		// Moves the client pitch angle towards cl.idealpitch sent by the server.
		//
		// If the user is adjusting pitch manually, either with lookup/lookdown,
		// mlook and mouse, or klook and keyboard, pitch drifting is constantly stopped.
		//
		// Drifting is enabled when the center view key is hit, mlook is released and
		// lookspring is non 0, or when
		private void DriftPitch( )
		{
			OnApplyDriftPitch?.Invoke( );
		}

		// V_BoundOffsets
		private void BoundOffsets( )
		{
			var ent = _clientState.ViewEntity;

			// absolutely bound refresh reletive to entity clipping hull
			// so the view can never be inside a solid wall
			var rdef = _renderState.Data;
			if ( rdef.vieworg.X < ent.origin.X - 14 )
				rdef.vieworg.X = ent.origin.X - 14;
			else if ( rdef.vieworg.X > ent.origin.X + 14 )
				rdef.vieworg.X = ent.origin.X + 14;

			if ( rdef.vieworg.Y < ent.origin.Y - 14 )
				rdef.vieworg.Y = ent.origin.Y - 14;
			else if ( rdef.vieworg.Y > ent.origin.Y + 14 )
				rdef.vieworg.Y = ent.origin.Y + 14;

			if ( rdef.vieworg.Z < ent.origin.Z - 22 )
				rdef.vieworg.Z = ent.origin.Z - 22;
			else if ( rdef.vieworg.Z > ent.origin.Z + 30 )
				rdef.vieworg.Z = ent.origin.Z + 30;
		}

		/// <summary>
		/// CalcGunAngle
		/// </summary>
		private void CalcGunAngle( )
		{
			var rdef = _renderState.Data;
			var yaw = rdef.viewangles.Y;
			var pitch = -rdef.viewangles.X;

			yaw = Utilities.AngleDelta( yaw - rdef.viewangles.Y ) * 0.4f;
			if ( yaw > 10 )
				yaw = 10;
			if ( yaw < -10 )
				yaw = -10;
			pitch = Utilities.AngleDelta( -pitch - rdef.viewangles.X ) * 0.4f;
			if ( pitch > 10 )
				pitch = 10;
			if ( pitch < -10 )
				pitch = -10;
			var move = ( Single ) Time.Delta * 20;
			if ( yaw > _OldYaw )
			{
				if ( _OldYaw + move < yaw )
					yaw = _OldYaw + move;
			}
			else
			{
				if ( _OldYaw - move > yaw )
					yaw = _OldYaw - move;
			}

			if ( pitch > _OldPitch )
			{
				if ( _OldPitch + move < pitch )
					pitch = _OldPitch + move;
			}
			else
			{
				if ( _OldPitch - move > pitch )
					pitch = _OldPitch - move;
			}

			_OldYaw = yaw;
			_OldPitch = pitch;

			var cl = _clientState.Data;
			cl.viewent.angles.Y = rdef.viewangles.Y + yaw;
			cl.viewent.angles.X = -( rdef.viewangles.X + pitch );

			var idleScale = Cvars.IdleScale.Get<Single>();
			cl.viewent.angles.Z -= ( Single ) ( idleScale * Math.Sin( cl.time * Cvars.IRollCycle.Get<Single>() ) * Cvars.IRollLevel.Get<Single>() );
			cl.viewent.angles.X -= ( Single ) ( idleScale * Math.Sin( cl.time * Cvars.IPitchCycle.Get<Single>() ) * Cvars.IPitchLevel.Get<Single>() );
			cl.viewent.angles.Y -= ( Single ) ( idleScale * Math.Sin( cl.time * Cvars.IYawCycle.Get<Single>() ) * Cvars.IYawLevel.Get<Single>() );
		}


		// V_CalcPowerupCshift
		private void CalcPowerupCshift( )
		{
			var cl = _clientState.Data;
			if ( cl.HasItems( QItemsDef.IT_QUAD ) )
			{
				cl.cshifts[ColourShiftDef.CSHIFT_POWERUP].destcolor[0] = 0;
				cl.cshifts[ColourShiftDef.CSHIFT_POWERUP].destcolor[1] = 0;
				cl.cshifts[ColourShiftDef.CSHIFT_POWERUP].destcolor[2] = 255;
				cl.cshifts[ColourShiftDef.CSHIFT_POWERUP].percent = 30;
			}
			else if ( cl.HasItems( QItemsDef.IT_SUIT ) )
			{
				cl.cshifts[ColourShiftDef.CSHIFT_POWERUP].destcolor[0] = 0;
				cl.cshifts[ColourShiftDef.CSHIFT_POWERUP].destcolor[1] = 255;
				cl.cshifts[ColourShiftDef.CSHIFT_POWERUP].destcolor[2] = 0;
				cl.cshifts[ColourShiftDef.CSHIFT_POWERUP].percent = 20;
			}
			else if ( cl.HasItems( QItemsDef.IT_INVISIBILITY ) )
			{
				cl.cshifts[ColourShiftDef.CSHIFT_POWERUP].destcolor[0] = 100;
				cl.cshifts[ColourShiftDef.CSHIFT_POWERUP].destcolor[1] = 100;
				cl.cshifts[ColourShiftDef.CSHIFT_POWERUP].destcolor[2] = 100;
				cl.cshifts[ColourShiftDef.CSHIFT_POWERUP].percent = 100;
			}
			else if ( cl.HasItems( QItemsDef.IT_INVULNERABILITY ) )
			{
				cl.cshifts[ColourShiftDef.CSHIFT_POWERUP].destcolor[0] = 255;
				cl.cshifts[ColourShiftDef.CSHIFT_POWERUP].destcolor[1] = 255;
				cl.cshifts[ColourShiftDef.CSHIFT_POWERUP].destcolor[2] = 0;
				cl.cshifts[ColourShiftDef.CSHIFT_POWERUP].percent = 30;
			}
			else
				cl.cshifts[ColourShiftDef.CSHIFT_POWERUP].percent = 0;
		}

		// V_CheckGamma
		private Boolean CheckGamma( )
		{
			if ( Cvars.Gamma.Get<Single>() == _OldGammaValue )
				return false;

			_OldGammaValue = Cvars.Gamma.Get<Single>();

			BuildGammaTable( Cvars.Gamma.Get<Single>() );
			_videoState.Data.recalc_refdef = true;   // force a surface cache flush

			return true;
		}

		// VID_ShiftPalette from gl_vidnt.c
		private void ShiftPalette( Byte[] palette )
		{
			//	VID_SetPalette (palette);
			//	gammaworks = SetDeviceGammaRamp (maindc, ramps);
		}
	}
}