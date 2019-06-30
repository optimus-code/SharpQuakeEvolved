/// <copyright>
///
/// Rewritten in C# by Yury Kiselev, 2010.
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
using OpenTK;
using OpenTK.Graphics.OpenGL;

// gr_rlights.c

namespace SharpQuake
{
    partial class render
    {
        private static Int32 _DlightFrameCount; // r_dlightframecount
        private static mplane_t _LightPlane; // lightplane

        /// <summary>
        /// R_PushDlights
        /// </summary>
        public static void PushDlights()
        {
            if( _glFlashBlend.Value != 0 )
                return;

            _DlightFrameCount = _FrameCount + 1;	// because the count hasn't advanced yet for this frame

            for( var i = 0; i < client.MAX_DLIGHTS; i++ )
            {
                dlight_t l = client.DLights[i];
                if( l.die < client.cl.time || l.radius == 0 )
                    continue;
                render.MarkLights( l, 1 << i, client.cl.worldmodel.nodes[0] );
            }
        }

        /// <summary>
        /// R_MarkLights
        /// </summary>
        private static void MarkLights( dlight_t light, Int32 bit, mnodebase_t node )
        {
            if( node.contents < 0 )
                return;

            mnode_t n = (mnode_t)node;
            mplane_t splitplane = n.plane;
            var dist = Vector3.Dot( light.origin, splitplane.normal ) - splitplane.dist;

            if( dist > light.radius )
            {
                MarkLights( light, bit, n.children[0] );
                return;
            }
            if( dist < -light.radius )
            {
                MarkLights( light, bit, n.children[1] );
                return;
            }

            // mark the polygons
            for( var i = 0; i < n.numsurfaces; i++ )
            {
                msurface_t surf = client.cl.worldmodel.surfaces[n.firstsurface + i];
                if( surf.dlightframe != _DlightFrameCount )
                {
                    surf.dlightbits = 0;
                    surf.dlightframe = _DlightFrameCount;
                }
                surf.dlightbits |= bit;
            }

            MarkLights( light, bit, n.children[0] );
            MarkLights( light, bit, n.children[1] );
        }

        /// <summary>
        /// R_RenderDlights
        /// </summary>
        private static void RenderDlights()
        {
            //int i;
            //dlight_t* l;

            if( _glFlashBlend.Value == 0 )
                return;

            _DlightFrameCount = _FrameCount + 1;	// because the count hasn't advanced yet for this frame

            GL.DepthMask( false );
            GL.Disable( EnableCap.Texture2D );
            GL.ShadeModel( ShadingModel.Smooth );
            GL.Enable( EnableCap.Blend );
            GL.BlendFunc( BlendingFactor.One, BlendingFactor.One );

            for( var i = 0; i < client.MAX_DLIGHTS; i++ )
            {
                dlight_t l = client.DLights[i];
                if( l.die < client.cl.time || l.radius == 0 )
                    continue;

                RenderDlight( l );
            }

            GL.Color3( 1f, 1, 1 );
            GL.Disable( EnableCap.Blend );
            GL.Enable( EnableCap.Texture2D );
            GL.BlendFunc( BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha );
            GL.DepthMask( true );
        }

        /// <summary>
        /// R_AnimateLight
        /// </summary>
        private static void AnimateLight()
        {
            //
            // light animations
            // 'm' is normal light, 'a' is no light, 'z' is double bright
            var i = ( Int32 ) ( client.cl.time * 10 );
            for( var j = 0; j < QDef.MAX_LIGHTSTYLES; j++ )
            {
                if( String.IsNullOrEmpty( client.LightStyle[j].map ) )
                {
                    _LightStyleValue[j] = 256;
                    continue;
                }
                var map = client.LightStyle[j].map;
                var k = i % map.Length;
                k = map[k] - 'a';
                k = k * 22;
                _LightStyleValue[j] = k;
            }
        }

        /// <summary>
        /// R_LightPoint
        /// </summary>
        private static Int32 LightPoint( ref Vector3 p )
        {
            if( client.cl.worldmodel.lightdata == null )
                return 255;

            Vector3 end = p;
            end.Z -= 2048;

            var r = RecursiveLightPoint( client.cl.worldmodel.nodes[0], ref p, ref end );
            if( r == -1 )
                r = 0;

            return r;
        }

        private static Int32 RecursiveLightPoint( mnodebase_t node, ref Vector3 start, ref Vector3 end )
        {
            if( node.contents < 0 )
                return -1;		// didn't hit anything

            mnode_t n = (mnode_t)node;

            // calculate mid point

            // FIXME: optimize for axial
            mplane_t plane = n.plane;
            var front = Vector3.Dot( start, plane.normal ) - plane.dist;
            var back = Vector3.Dot( end, plane.normal ) - plane.dist;
            var side = front < 0 ? 1 : 0;

            if( ( back < 0 ? 1 : 0 ) == side )
                return RecursiveLightPoint( n.children[side], ref start, ref end );

            var frac = front / ( front - back );
            Vector3 mid = start + ( end - start ) * frac;

            // go down front side
            var r = RecursiveLightPoint( n.children[side], ref start, ref mid );
            if( r >= 0 )
                return r;		// hit something

            if( ( back < 0 ? 1 : 0 ) == side )
                return -1;		// didn't hit anuthing

            // check for impact on this node
            _LightSpot = mid;
            _LightPlane = plane;

            msurface_t[] surf = client.cl.worldmodel.surfaces;
            Int32 offset = n.firstsurface;
            for( var i = 0; i < n.numsurfaces; i++, offset++ )
            {
                if( ( surf[offset].flags & Surf.SURF_DRAWTILED ) != 0 )
                    continue;	// no lightmaps

                mtexinfo_t tex = surf[offset].texinfo;

                var s = ( Int32 ) ( Vector3.Dot( mid, tex.vecs[0].Xyz ) + tex.vecs[0].W );
                var t = ( Int32 ) ( Vector3.Dot( mid, tex.vecs[1].Xyz ) + tex.vecs[1].W );

                if( s < surf[offset].texturemins[0] || t < surf[offset].texturemins[1] )
                    continue;

                var ds = s - surf[offset].texturemins[0];
                var dt = t - surf[offset].texturemins[1];

                if( ds > surf[offset].extents[0] || dt > surf[offset].extents[1] )
                    continue;

                if( surf[offset].sample_base == null )
                    return 0;

                ds >>= 4;
                dt >>= 4;

                Byte[] lightmap = surf[offset].sample_base;
                var lmOffset = surf[offset].sampleofs;
                Int16[] extents = surf[offset].extents;
                r = 0;
                if( lightmap != null )
                {
                    lmOffset += dt * ( ( extents[0] >> 4 ) + 1 ) + ds;

                    for( var maps = 0; maps < bsp_file.MAXLIGHTMAPS && surf[offset].styles[maps] != 255; maps++ )
                    {
                        var scale = _LightStyleValue[surf[offset].styles[maps]];
                        r += lightmap[lmOffset] * scale;
                        lmOffset += ( ( extents[0] >> 4 ) + 1 ) * ( ( extents[1] >> 4 ) + 1 );
                    }

                    r >>= 8;
                }

                return r;
            }

            // go down back side
            return RecursiveLightPoint( n.children[side == 0 ? 1 : 0], ref mid, ref end );
        }

        /// <summary>
        /// R_RenderDlight
        /// </summary>
        private static void RenderDlight( dlight_t light )
        {
            var rad = light.radius * 0.35f;
            Vector3 v = light.origin - render.Origin;
            if( v.Length < rad )
            {	// view is inside the dlight
                AddLightBlend( 1, 0.5f, 0, light.radius * 0.0003f );
                return;
            }

            GL.Begin( PrimitiveType.TriangleFan );
            GL.Color3( 0.2f, 0.1f, 0 );
            v = light.origin - render.ViewPn * rad;
            GL.Vertex3( v );
            GL.Color3( 0, 0, 0 );
            for( var i = 16; i >= 0; i-- )
            {
                var a = i / 16.0 * Math.PI * 2;
                v = light.origin + render.ViewRight * ( Single ) Math.Cos( a ) * rad + render.ViewUp * ( Single ) Math.Sin( a ) * rad;
                GL.Vertex3( v );
            }
            GL.End();
        }

        private static void AddLightBlend( Single r, Single g, Single b, Single a2 )
        {
            view.Blend.A += a2 * ( 1 - view.Blend.A );

            var a = view.Blend.A;

            a2 = a2 / a;

            view.Blend.R = view.Blend.R * ( 1 - a2 ) + r * a2; // error? - v_blend[0] = v_blend[1] * (1 - a2) + r * a2;
            view.Blend.G = view.Blend.G * ( 1 - a2 ) + g * a2;
            view.Blend.B = view.Blend.B * ( 1 - a2 ) + b * a2;
        }
    }
}