using SharpFont;
using SharpQuake.Framework.Mathematics;
using SharpQuake.Game.Data.Models;
using SharpQuake.Game.Rendering.Memory;
using SharpQuake.Game.World;

namespace SharpQuake.Rendering.Environment.Surface
{
    internal class WorldSurfaceCollector( render renderer,
        RenderState renderState )
    {
        private readonly render _renderer = renderer;
        private readonly RenderState _renderState = renderState;

        public void Collect( Entity tempEnt )
        {
            RecursiveWorldNode( ( ( BrushModelData ) tempEnt.model ).Nodes[0], _renderState.Data.vieworg );
        }

        /// <summary>
		/// R_RecursiveWorldNode
		/// </summary>
		private void RecursiveWorldNode( MemoryNodeBase node, Vector3 modelOrigin )
        {
            _renderer.World.Occlusion.RecursiveWorldNode(
                node,
                modelOrigin,
                _renderer.World.Lighting.FrameCount,
                _renderer.Frustum,
                ( surf ) =>
                {
                    //DrawSequentialPoly( surf );
                }, ( efrags ) =>
                {
                    _renderer.World.Entities.StoreEfrags( efrags );
                } );
        }
    }
}
