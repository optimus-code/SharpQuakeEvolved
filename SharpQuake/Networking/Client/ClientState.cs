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
using SharpQuake.Game.Client;
using SharpQuake.Game.World;
using System;

namespace SharpQuake.Networking.Client
{
    public class ClientState
    {
        public client_state_t Data
        {
            get;
            set;
        } = new client_state_t();

        public client_static_t StaticData
        {
            get;
            set;
        } = new client_static_t( );

        public Int32 NumVisEdicts
        {
            get;
            set;
        }

        public EFrag[] EFrags
        {
            get;
            set;
        } = new EFrag[ClientDef.MAX_EFRAGS]; // cl_efrags

        public Entity[] Entities
        {
            get;
            set;
        } = new Entity[QDef.MAX_EDICTS]; // cl_entities

        public Entity[] StaticEntities
        {
            get;
            set;
        } = new Entity[ClientDef.MAX_STATIC_ENTITIES]; // cl_static_entities

        public lightstyle_t[] LightStyle
        {
            get;
            set;
        } = new lightstyle_t[QDef.MAX_LIGHTSTYLES]; // cl_lightstyle

        public dlight_t[] DLights
        {
            get;
            set;
        } = new dlight_t[ClientDef.MAX_DLIGHTS]; // cl_dlights

        // cl_numvisedicts
        public Entity[] VisEdicts
        {
            get;
            set;
        } = new Entity[ClientDef.MAX_VISEDICTS]; // cl_visedicts[MAX_VISEDICTS]

        /// <summary>
		/// cl_entities[_state.Data.viewentity]
		/// Player model (visible when out of body)
		/// </summary>
		public Entity ViewEntity
        {
            get
            {
                return Entities[Data.viewentity];
            }
        }

        /// <summary>
        /// _state.Data.viewent
        /// Weapon model (only visible from inside body)
        /// </summary>
        public Entity ViewEnt
        {
            get
            {
                return Data.viewent;
            }
        }

        public Int32 NumTempEntities
        {
            get;
            set;
        } // num_temp_entities

        public Entity[] TempEntities
        {
            get;
            set;
        } = new Entity[ClientDef.MAX_TEMP_ENTITIES]; // cl_temp_entities[MAX_TEMP_ENTITIES]

        public beam_t[] Beams
        {
            get;
            set;
        } = new beam_t[ClientDef.MAX_BEAMS]; // cl_beams[MAX_BEAMS]

        public ClientState()
        {
            for ( var i = 0; i < EFrags.Length; i++ )
                EFrags[i] = new EFrag( );

            for ( var i = 0; i < Entities.Length; i++ )
                Entities[i] = new Entity( );

            for ( var i = 0; i < StaticEntities.Length; i++ )
                StaticEntities[i] = new Entity( );

            for ( var i = 0; i < DLights.Length; i++ )
                DLights[i] = new dlight_t( );

            for ( var i = 0; i < TempEntities.Length; i++ )
                TempEntities[i] = new Entity( );

            for ( var i = 0; i < Beams.Length; i++ )
                Beams[i] = new beam_t( );
        }

        /// <summary>  
        /// CL_ClearState
        /// </summary>
        public void Clear( )
        {
            // wipe the entire cl structure
            Data.Clear( );

            StaticData.message.Clear( );

            // clear other arrays
            foreach ( var ef in EFrags )
                ef.Clear( );

            foreach ( var et in Entities )
                et.Clear( );

            foreach ( var dl in DLights )
                dl.Clear( );

            Array.Clear( LightStyle, 0, LightStyle.Length );

            foreach ( var et in TempEntities )
                et.Clear( );

            foreach ( var b in Beams )
                b.Clear( );

            //
            // allocate the efrags and chain together into a free list
            //
            Data.free_efrags = EFrags[0];// cl_efrags;

            for ( var i = 0; i < ClientDef.MAX_EFRAGS - 1; i++ )
                EFrags[i].entnext = EFrags[i + 1];

           EFrags[ClientDef.MAX_EFRAGS - 1].entnext = null;
        }

        /// <summary>
		/// CL_AllocDlight
		/// </summary>
		public dlight_t AllocDlight( Int32 key )
        {
            dlight_t dl;

            // first look for an exact key match
            if ( key != 0 )
            {
                for ( var i = 0; i < ClientDef.MAX_DLIGHTS; i++ )
                {
                    dl = DLights[i];
                    if ( dl.key == key )
                    {
                        dl.Clear( );
                        dl.key = key;
                        return dl;
                    }
                }
            }

            // then look for anything else
            //dl = cl_dlights;
            for ( var i = 0; i < ClientDef.MAX_DLIGHTS; i++ )
            {
                dl = DLights[i];
                if ( dl.die < Data.time )
                {
                    dl.Clear( );
                    dl.key = key;
                    return dl;
                }
            }

            dl = DLights[0];
            dl.Clear( );
            dl.key = key;
            return dl;
        }

        /// <summary>
        /// CL_DecayLights
        /// </summary>
        public void DecayLights( )
        {
            var time = ( Single ) ( Data.time - Data.oldtime );

            for ( var i = 0; i < ClientDef.MAX_DLIGHTS; i++ )
            {
                var dl = DLights[i];

                if ( dl.die < Data.time || dl.radius == 0 )
                    continue;

                dl.radius -= time * dl.decay;
                if ( dl.radius < 0 )
                    dl.radius = 0;
            }
        }

        /// <summary>
        /// CL_NewTempEntity
        /// </summary>
        public Entity NewTempEntity( byte[] colormap )
        {
            if ( NumVisEdicts == ClientDef.MAX_VISEDICTS )
                return null;

            if ( NumTempEntities == ClientDef.MAX_TEMP_ENTITIES )
                return null;

            var ent = TempEntities[NumTempEntities];
            NumTempEntities++;
            VisEdicts[NumVisEdicts] = ent;
            NumVisEdicts++;

            ent.colormap = colormap;

            return ent;
        }
    }
}
