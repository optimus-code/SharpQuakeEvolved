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

using SharpQuake.Factories.Rendering;
using SharpQuake.Framework.Logging;
using SharpQuake.Game.World;
using SharpQuake.Networking.Client;
using SharpQuake.Renderer;
using SharpQuake.Rendering.Cameras;

namespace SharpQuake.Rendering.Environment
{
    public class World
    {
        public Entity WorldEntity
        {
            get;
            private set; 
        }

        public Occlusion Occlusion
        {
            get;
            private set;
        }

        public ParticleSystem Particles
        {
            get;
            private set;
        }

        public Lighting Lighting
        {
            get;
            private set;
        }

        public Sky Sky
        {
            get;
            private set;
        }
        public Entities Entities
        {
            get;
            private set;
        }

        private readonly IConsoleLogger _logger;
        private readonly Vid _video;
        private readonly ClientState _clientState;
        private readonly Drawer _drawer;
        private readonly render _renderer;
        private readonly View _view;
        private readonly ChaseView _chaseView;
        private readonly ModelFactory _models;

        public World( IConsoleLogger logger, Vid video, VideoState videoState, ClientState clientState, Drawer drawer, 
            render renderer, View view, ModelFactory models, IGameRenderer gameRenderer,
            RenderState renderState ) 
        {
            _logger = logger;
            _video = video;
            _clientState = clientState;
            _drawer = drawer;
            _renderer = renderer;
            _view = view;
            _chaseView = _view.ChaseView;
            _models = models;

            Sky = new Sky( _clientState );
            Lighting = new Lighting( _clientState, _drawer, _video, _renderer, _view, videoState );
            WorldEntity = new Entity( );
            Particles = new ParticleSystem( _video.Device );
            Entities = new Entities( _logger, _clientState, _renderer, _video, _drawer, _chaseView, _models, gameRenderer, renderState );
        }

        public void Initialise( TextureChains textureChains )
        {
            Occlusion = new Occlusion( _clientState, textureChains );
        }

        /// <summary>
        /// R_NewMap
        /// </summary>
        public void NewMap()
        {
            Lighting.Reset( );

            WorldEntity.Clear( );
            WorldEntity.model = _clientState.Data.worldmodel;

            // clear out efrags in case the level hasn't been reloaded
            // FIXME: is this one short?
            for ( var i = 0; i < _clientState.Data.worldmodel.NumLeafs; i++ )
                _clientState.Data.worldmodel.Leaves[i].efrags = null;

            Occlusion.ViewLeaf = null;
            Particles.Clear( );

            Lighting.BuildLightMaps( );

            Sky.Identify( );
        }
    }
}
