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
using SharpQuake.Framework.Mathematics;
using SharpQuake.Framework.World;
using SharpQuake.Networking.Client;
using SharpQuake.Sys;

// chase.c -- chase camera code

namespace SharpQuake.Rendering.Cameras
{
    /// <summary>
    /// Chase_functions
    /// </summary>
    public class ChaseView
    {
        /// <summary>
        /// chase_active.value != 0
        /// </summary>
        public Boolean IsActive
        {
            get
            {
                return ( Cvars.Active.Get<Boolean>( ) );
            }
        }

        private Vector3 _Dest;

        private readonly ClientVariableFactory _cvars;
        private readonly RenderState _renderState;
        private readonly ClientState _clientState;
        private readonly ServerWorld _serverWorld;

        public ChaseView( ClientVariableFactory cvars, RenderState renderState, ClientState clientState, ServerWorld serverWorld )
        {
            _cvars = cvars;
            _renderState = renderState;
            _clientState = clientState;
            _serverWorld = serverWorld;
        }

        // Chase_Init
        public void Initialise()
        {
            if( Cvars.Back == null )
            {
                Cvars.Back = _cvars.Add( "chase_back", 100f );
                Cvars.Up = _cvars.Add( "chase_up", 16f );
                Cvars.Right = _cvars.Add( "chase_right", 0f );
                Cvars.Active = _cvars.Add( "chase_active", false );
            }
        }

        // Chase_Reset
        public void Reset()
        {
            // for respawning and teleporting
            //	start position 12 units behind head
        }

        // Chase_Update
        public void Update()
        {
            // if can't see player, reset
            Vector3 forward, up, right;
            MathLib.AngleVectors( ref _clientState.Data.viewangles, out forward, out right, out up );

            // calc exact destination
            _Dest = _renderState.Data.vieworg - forward * Cvars.Back.Get<Single>( ) - right * Cvars.Right.Get<Single>( );
            _Dest.Z = _renderState.Data.vieworg.Z + Cvars.Up.Get<Single>( );

            // find the spot the player is looking at
            var dest = _renderState.Data.vieworg + forward * 4096;

            Vector3 stop;
            TraceLine( ref _renderState.Data.vieworg, ref dest, out stop );

            // calculate pitch to look at the same spot from camera
            stop -= _renderState.Data.vieworg;
            Single dist;
            Vector3.Dot( ref stop, ref forward, out dist );
            if( dist < 1 )
                dist = 1;

            _renderState.Data.viewangles.X = ( Single ) ( -Math.Atan( stop.Z / dist ) / Math.PI * 180.0 );
            //r_refdef.viewangles[PITCH] = -atan(stop[2] / dist) / M_PI * 180;

            // move towards destination
            _renderState.Data.vieworg = _Dest; //VectorCopy(chase_dest, r_refdef.vieworg);
        }

        private void TraceLine( ref Vector3 start, ref Vector3 end, out Vector3 impact )
        {
            var trace = new Trace_t();

            _serverWorld.RecursiveHullCheck( _clientState.Data.worldmodel.Hulls[0], 0, 0, 1, ref start, ref end, trace );

            impact = trace.endpos; // VectorCopy(trace.endpos, impact);
        }
    }
}
