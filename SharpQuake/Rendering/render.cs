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
using SharpQuake.Factories.Rendering;
using SharpQuake.Framework;
using SharpQuake.Framework.Factories.IO;
using SharpQuake.Framework.IO;
using SharpQuake.Framework.Logging;
using SharpQuake.Framework.Mathematics;
using SharpQuake.Game.Data.Models;
using SharpQuake.Game.Rendering.Textures;
using SharpQuake.Game.World;
using SharpQuake.Networking.Client;
using SharpQuake.Renderer.Textures;
using SharpQuake.Rendering;
using SharpQuake.Rendering.Environment;
using SharpQuake.Sys;

// refresh.h -- public interface to refresh functions
// gl_rmisc.c
// gl_rmain.c

namespace SharpQuake
{
	/// <summary>
	/// R_functions
	/// </summary>
	public partial class render
    {
        public Boolean CacheTrash
        {
            get
            {
                return _CacheThrash;
            }
        }


        public const Int32 MAXCLIPPLANES = 11;
        public const Int32 TOP_RANGE = 16;			// soldier uniform colors
        public const Int32 BOTTOM_RANGE = 96;

        //
        // view origin
        //
        public Vector3 ViewUp;

        // vup
        public Vector3 ViewPn;

        // vpn
        public Vector3 ViewRight;

        // vright
        public Vector3 Origin;

        private BaseTexture[] PlayerTextures;
        private System.Boolean _CacheThrash; // r_cache_thrash	// compatability

        // r_origin

        private Entity _CurrentEntity; // currententity

                                      //private Int32 _MirrorTextureNum; // mirrortexturenum	// quake texturenum, not gltexturenum

       

        public World World
        {
            get;
            private set;
        }

        //private System.Boolean _IsMirror; // mirror
        //private Plane _MirrorPlane; // mirror_plane

        // Temporarily turn into property until GL stripped out of this project
        private Single _glDepthMin
        {
            get
            {
                return _video.Device.Desc.DepthMinimum;
            }
            set
            {
                _video.Device.Desc.DepthMinimum = value;
            }
        }

        private Single _glDepthMax
        {
            get
            {
                return _video.Device.Desc.DepthMaximum;
            }
            set
            {
                _video.Device.Desc.DepthMaximum = value;
            }
        }

        public Plane[] Frustum
        {
            get;
            private set;
        } = new Plane[4]; // frustum

        private System.Boolean _IsEnvMap = false; // envmap	// true during envmap command capture
        private Vector3 _ModelOrg; // modelorg
        private Vector3 _EntOrigin; // r_entorigin
        private Single _ShadeLight; // shadelight
        private Single _AmbientLight; // ambientlight
        private Single[] _ShadeDots = anorm_dots.Values[0]; // shadedots
        private Vector3 _ShadeVector; // shadevector

        public TextureChains TextureChains
        {
            get;
            protected set;
        }


        private readonly IGameRenderer _gameRenderer;
        private readonly IConsoleLogger _logger;
        private readonly CommandFactory _commands;
        private readonly ClientVariableFactory _cvars;
        private readonly ModelFactory _models;
        private readonly Vid _video;
        private readonly ClientState _clientState;
        private readonly View _view;
        private readonly Drawer _drawer;
        private readonly MainWindow _window;
        private readonly snd _sound;
        private readonly RenderState _renderState;

        public render( IGameRenderer gameRenderer, IConsoleLogger logger, CommandFactory commands,
            ClientVariableFactory cvars, ModelFactory models, Vid video, VideoState videoState, 
            ClientState clientState, Drawer drawer, MainWindow window, snd sound, View view,
            RenderState renderState )
        {
            _gameRenderer = gameRenderer;
            _logger = logger;
            _commands = commands;
            _cvars = cvars;
            _models = models;
            _video = video;
            _clientState = clientState;
            _drawer = drawer;
            _window = window;
            _sound = sound;
            _view = view;
            _renderState = renderState;
            _renderState.OnRender += Render;
            _renderState.OnUpdatePalette += UpdatePalette;
            _renderState.OnRenderView += RenderView;

            World = new World( _logger, _video, videoState, _clientState, _drawer, this, _view, _models, _gameRenderer, renderState );
            _renderState.OnPushDLights += World.Lighting.PushDlights; 
            
            _gameRenderer.WarpableTextures = new WarpableTextures( _video.Device );
        }

        private void InitialiseClientVariables()
		{
            if ( Cvars.NoRefresh == null )
            {
                Cvars.NoRefresh = _cvars.Add( "r_norefresh", false );
                Cvars.DrawEntities = _cvars.Add( "r_drawentities", true );
                Cvars.DrawViewModel = _cvars.Add( "r_drawviewmodel", true );
                Cvars.Speeds = _cvars.Add( "r_speeds", false );
                Cvars.FullBright = _cvars.Add( "r_fullbright", false );
                Cvars.LightMap = _cvars.Add( "r_lightmap", false );
                Cvars.Shadows = _cvars.Add( "r_shadows", false );
                //_MirrorAlpha = _cvars.Add( "r_mirroralpha", "1" );
                Cvars.WaterAlpha = _cvars.Add( "r_wateralpha", 0.7f );
                Cvars.Dynamic = _cvars.Add( "r_dynamic", true );
                Cvars.NoVis = _cvars.Add( "r_novis", false );

                Cvars.NoiseGrain = _cvars.Add( "r_noisegrain", 0.025f );
                Cvars.Bloom = _cvars.Add( "r_bloom", 0.26f );

                Cvars.glFinish = _cvars.Add( "gl_finish", false );
                Cvars.glClear = _cvars.Add( "gl_clear", 0f );
                Cvars.glCull = _cvars.Add( "gl_cull", true );
                Cvars.glTexSort = _cvars.Add( "gl_texsort", true );
                Cvars.glSmoothModels = _cvars.Add( "gl_smoothmodels", true );
                Cvars.glAffineModels = _cvars.Add( "gl_affinemodels", false );
                Cvars.glPolyBlend = _cvars.Add( "gl_polyblend", true );
                Cvars.glFlashBlend = _cvars.Add( "gl_flashblend", false );
                Cvars.glPlayerMip = _cvars.Add( "gl_playermip", 0 );
                Cvars.glNoColors = _cvars.Add( "gl_nocolors", false );
                Cvars.glKeepTJunctions = _cvars.Add( "gl_keeptjunctions", false );
                Cvars.glReportTJunctions = _cvars.Add( "gl_reporttjunctions", false );
                Cvars.glDoubleEyes = _cvars.Add( "gl_doubleeys", true );
            }

            if ( _video.Device.Desc.SupportsMultiTexture )
                _cvars.Set( "gl_texsort", 0.0f );
        }

        /// <summary>
        /// R_Init
        /// </summary>
        public void Initialise( )
        {            
            for ( var i = 0; i < Frustum.Length; i++ )
                Frustum[i] = new Plane( );

            _commands.Add( "timerefresh", TimeRefresh_f );
            //Cmd.Add("envmap", Envmap_f);
            //Cmd.Add("pointfile", ReadPointFile_f);

            InitialiseClientVariables();

            World.Particles.InitParticles( );

            // reserve 16 textures
            PlayerTextures = new BaseTexture[16];

            for ( var i = 0; i < PlayerTextures.Length; i++ )
            {
                PlayerTextures[i] = BaseTexture.FromDynamicBuffer( _video.Device, "_PlayerTexture{i}", new ByteArraySegment( new Byte[512 * 256 * 4] ), 512, 256, false, false );
            }

            TextureChains = new TextureChains();
            World.Initialise( TextureChains );
        }

        /// <summary>
        /// R_RenderView
        /// r_refdef must be set before the first call
        /// </summary>
        public void RenderView( )
        {
            if ( Cvars.NoRefresh.Get<Boolean>() )
                return;

            if ( World.WorldEntity.model == null || _clientState.Data.worldmodel == null )
                Utilities.Error( "R_RenderView: NULL worldmodel" );

            Double time1 = 0;
            if ( Cvars.Speeds.Get<Boolean>( ) )
            {
                _video.Device.Finish( );
                time1 = Timer.GetFloatTime( );
                World.Entities.Surfaces.Reset( );
                World.Entities.Reset( );
            }

            //_IsMirror = false;

            if ( Cvars.glFinish.Get<Boolean>() )
                _video.Device.Finish( );

            Clear( );

            // render normal view

            RenderScene( );


            // render mirror view
            //Mirror();

            PolyBlend( );

            if ( Cvars.Speeds.Get<Boolean>() )
            {
                var time2 = Timer.GetFloatTime( );
                ConsoleWrapper.Print( "{0,3} ms  {1,4} wpoly {2,4} epoly\n", ( Int32 ) ( ( time2 - time1 ) * 1000 ), World.Entities.Surfaces.BrushPolys, World.Entities.AliasPolys );
            }
        }

        /// <summary>
        /// R_TranslatePlayerSkin
        /// Translates a skin texture by the per-player color lookup
        /// </summary>
        public void TranslatePlayerSkin( Int32 playernum )
        {
            _video.Device.DisableMultitexture( );

            var top = _clientState.Data.scores[playernum].colors & 0xf0;
            var bottom = ( _clientState.Data.scores[playernum].colors & 15 ) << 4;

            var translate = new Byte[256];
            for ( var i = 0; i < 256; i++ )
                translate[i] = ( Byte ) i;

            for ( var i = 0; i < 16; i++ )
            {
                if ( top < 128 )	// the artists made some backwards ranges.  sigh.
                    translate[TOP_RANGE + i] = ( Byte ) ( top + i );
                else
                    translate[TOP_RANGE + i] = ( Byte ) ( top + 15 - i );

                if ( bottom < 128 )
                    translate[BOTTOM_RANGE + i] = ( Byte ) ( bottom + i );
                else
                    translate[BOTTOM_RANGE + i] = ( Byte ) ( bottom + 15 - i );
            }

            //
            // locate the original skin pixels
            //
            _CurrentEntity = _clientState.Entities[1 + playernum];
            var model = _CurrentEntity.model;
            if ( model == null )
                return;		// player doesn't have a model yet
            if ( model.Type != ModelType.Alias )
                return; // only translate skins on alias models

            var paliashdr = _models.GetExtraData( model );
            var s = paliashdr.skinwidth * paliashdr.skinheight;
            if ( ( s & 3 ) != 0 )
                Utilities.Error( "R_TranslateSkin: s&3" );

            Byte[] original;
            if ( _CurrentEntity.skinnum < 0 || _CurrentEntity.skinnum >= paliashdr.numskins )
            {
                ConsoleWrapper.Print( "({0}): Invalid player skin #{1}\n", playernum, _CurrentEntity.skinnum );
                original = ( Byte[] ) paliashdr.texels[0];// (byte *)paliashdr + paliashdr.texels[0];
            }
            else
                original = ( Byte[] ) paliashdr.texels[_CurrentEntity.skinnum];

            var inwidth = paliashdr.skinwidth;
            var inheight = paliashdr.skinheight;

            // because this happens during gameplay, do it fast
            // instead of sending it through gl_upload 8
            var maxSize = Cvars.glMaxSize.Get<Int32>();
            PlayerTextures[playernum].TranslateAndUpload( original, translate, inwidth, inheight, maxSize, maxSize, ( Int32 ) Cvars.glPlayerMip.Get<Int32>() );
        }

        /// <summary>
        /// R_PolyBlend
        /// </summary>
        private void PolyBlend( )
        {
            if ( !Cvars.glPolyBlend.Get<Boolean>() )
                return;

            if ( _view.Blend.A == 0 )
                return;

            _video.Device.Graphics.PolyBlend( _view.Blend );
        }

        /// <summary>
        /// R_Mirror
        /// </summary>
        //private void Mirror()
        //{
        //    if( !_IsMirror )
        //        return;

        //    _BaseWorldMatrix = _WorldMatrix;

        //    var d = Vector3.Dot( _RefDef.vieworg, _MirrorPlane.normal ) - _MirrorPlane.dist;
        //    _RefDef.vieworg += _MirrorPlane.normal * -2 * d;

        //    d = Vector3.Dot( ViewPn, _MirrorPlane.normal );
        //    ViewPn += _MirrorPlane.normal * -2 * d;

        //    _RefDef.viewangles = new Vector3( ( Single ) ( Math.Asin( ViewPn.Z ) / Math.PI * 180.0 ),
        //        ( Single ) ( Math.Atan2( ViewPn.Y, ViewPn.X ) / Math.PI * 180.0 ),
        //        -_RefDef.viewangles.Z );

        //    var ent = _client.ViewEntity;
        //    if( _client.NumVisEdicts < ClientDef.MAX_VISEDICTS )
        //    {
        //        _client.VisEdicts[_client.NumVisEdicts] = ent;
        //        _client.NumVisEdicts++;
        //    }

        //    _glDepthMin = 0.5f;
        //    _glDepthMax = 1;
        //    GL.DepthRange( _glDepthMin, _glDepthMax );
        //    GL.DepthFunc( DepthFunction.Lequal );

        //    RenderScene();
        //    DrawWaterSurfaces();

        //    _glDepthMin = 0;
        //    _glDepthMax = 0.5f;
        //    GL.DepthRange( _glDepthMin, _glDepthMax );
        //    GL.DepthFunc( DepthFunction.Lequal );

        //    // blend on top
        //    GL.Enable( EnableCap.Blend );
        //    GL.MatrixMode( MatrixMode.Projection );
        //    if( _MirrorPlane.normal.Z != 0 )
        //        GL.Scale( 1f, -1, 1 );
        //    else
        //        GL.Scale( -1f, 1, 1 );
        //    GL.CullFace( CullFaceMode.Front );
        //    GL.MatrixMode( MatrixMode.Modelview );

        //    GL.LoadMatrix( ref _BaseWorldMatrix );

        //    GL.Color4( 1, 1, 1, _MirrorAlpha.Value );
        //    var s = _clientState.Data.worldmodel.textures[_MirrorTextureNum].texturechain;
        //    for( ; s != null; s = s.texturechain )
        //        RenderBrushPoly( s );
        //    _clientState.Data.worldmodel.textures[_MirrorTextureNum].texturechain = null;
        //    GL.Disable( EnableCap.Blend );
        //    GL.Color4( 1f, 1, 1, 1 );
        //}

        /// <summary>
        /// R_RenderScene
        /// r_refdef must be set before the first call
        /// </summary>
        private void RenderScene( )
        {
            SetupFrame( );

            SetFrustum( );

            SetupGL( );

            World.Occlusion.MarkLeaves( );  // done here so we know if we're in water

            World.Entities.Surfaces.DrawWorld( );		// adds entities to the list

            _sound.ExtraUpdate( );	// don't let sound get messed up if going slow

            World.Entities.DrawEntitiesOnList( );

            _video.Device.DisableMultitexture( );

            World.Lighting.RenderDlights( );

            World.Particles.DrawParticles( _clientState.Data.time, _clientState.Data.oldtime, Cvars.Gravity.Get<Single>( ), Origin, ViewUp, ViewRight, ViewPn );


#if GLTEST
	        Test_Draw ();
#endif

            World.Entities.DrawViewModel( _IsEnvMap );
            World.Entities.Surfaces.DrawWaterSurfaces( );
        }

		/// <summary>
		/// R_SetupGL
		/// </summary>
		private void SetupGL( )
        {
            _video.Device.Setup3DScene( Cvars.glCull.Get<Boolean>(), _renderState.Data, _IsEnvMap );
        }

        /// <summary>
        /// R_SetFrustum
        /// </summary>
        private void SetFrustum( )
        {
            if ( _renderState.Data.fov_x == 90 )
            {
                // front side is visible
                Frustum[0].normal = ViewPn + ViewRight;
                Frustum[1].normal = ViewPn - ViewRight;

                Frustum[2].normal = ViewPn + ViewUp;
                Frustum[3].normal = ViewPn - ViewUp;
            }
            else
            {
                // rotate VPN right by FOV_X/2 degrees
                MathLib.RotatePointAroundVector( out Frustum[0].normal, ref ViewUp, ref ViewPn, -( 90 - _renderState.Data.fov_x / 2 ) );
                // rotate VPN left by FOV_X/2 degrees
                MathLib.RotatePointAroundVector( out Frustum[1].normal, ref ViewUp, ref ViewPn, 90 - _renderState.Data.fov_x / 2 );
                // rotate VPN up by FOV_X/2 degrees
                MathLib.RotatePointAroundVector( out Frustum[2].normal, ref ViewRight, ref ViewPn, 90 - _renderState.Data.fov_y / 2 );
                // rotate VPN down by FOV_X/2 degrees
                MathLib.RotatePointAroundVector( out Frustum[3].normal, ref ViewRight, ref ViewPn, -( 90 - _renderState.Data.fov_y / 2 ) );
            }

            for ( var i = 0; i < 4; i++ )
            {
                Frustum[i].type = PlaneDef.PLANE_ANYZ;
                Frustum[i].dist = Vector3.Dot( Origin, Frustum[i].normal );
                Frustum[i].signbits = ( Byte )  Frustum[i].SignbitsForPlane();
            }
        }

        /// <summary>
        /// R_SetupFrame
        /// </summary>
        private void SetupFrame( )
        {
            // don't allow cheats in multiplayer
            if ( _clientState.Data.maxclients > 1 )
                _cvars.Set( "r_fullbright", false );

            World.Lighting.UpdateAnimations();

            World.Lighting.FrameCount++;

            // build the transformation matrix for the given view angles
            Origin = _renderState.Data.vieworg;

            MathLib.AngleVectors( ref _renderState.Data.viewangles, out ViewPn, out ViewRight, out ViewUp );

            // current viewleaf
            World.Occlusion.SetupFrame( ref Origin );
            _view.SetContentsColor( World.Occlusion.ViewLeaf.contents );
            _view.CalcBlend( );

            _CacheThrash = false;
            World.Entities.Surfaces.Reset( );
            World.Entities.Reset( );
        }

        /// <summary>
        /// R_Clear
        /// </summary>
        private void Clear( )
        {
            _video.Device.Clear( _video.glZTrick, Cvars.glClear.Get<Single>( ) );
        }

        /// <summary>
        /// R_TimeRefresh_f
        /// For program optimization
        /// </summary>
        private void TimeRefresh_f( CommandMessage msg )
        {
            //GL.DrawBuffer(DrawBufferMode.Front);
            _video.Device.Finish( );

            var start = Timer.GetFloatTime( );
            for ( var i = 0; i < 128; i++ )
            {
                _renderState.Data.viewangles.Y = ( Single ) ( i / 128.0 * 360.0 );
                RenderView( );
                _window.Present( );
            }

            _video.Device.Finish( );
            var stop = Timer.GetFloatTime( );
            var time = stop - start;
            _logger.Print( "{0:F} seconds ({1:F1} fps)\n", time, 128 / time );

            _renderState.OnTimeRefresh?.Invoke( );
            //GL.DrawBuffer(DrawBufferMode.Back);
        }

        /// <summary>
        /// R_TextureAnimation
        /// Returns the proper texture for a given time and base texture
        /// </summary>
        public ModelTexture TextureAnimation( ModelTexture t )
        {
            if ( _CurrentEntity.frame != 0 )
            {
                if ( t.alternate_anims != null )
                    t = t.alternate_anims;
            }

            if ( t.anim_total == 0 )
                return t;

            var reletive = ( Int32 ) ( _clientState.Data.time * 10 ) % t.anim_total;
            var count = 0;
            while ( t.anim_min > reletive || t.anim_max <= reletive )
            {
                t = t.anim_next;
                if ( t == null )
                    Utilities.Error( "R_TextureAnimation: broken cycle" );
                if ( ++count > 100 )
                    Utilities.Error( "R_TextureAnimation: infinite cycle" );
            }

            return t;
        }

        // Defined here to prevent circular dependency with _view <-> _screen
        public void Render( )
        {
            _view.RenderView( );
        }

        // Defined here to prevent circular dependency with _view <-> _screen
        public void UpdatePalette( )
        {
            _view.UpdatePalette( );
        }
    }
}
