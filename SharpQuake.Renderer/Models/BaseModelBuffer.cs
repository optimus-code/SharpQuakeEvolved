/// <copyright>
///
/// SharpQuakeEvolved changes by optimus-code, 2019
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
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SharpQuake.Framework;
using SharpQuake.Framework.Mathematics;
using SharpQuake.Renderer.Textures;

namespace SharpQuake.Renderer.Models
{
    public abstract class BaseModelBuffer : IDisposable
    {
        public BufferVertex[] Vertices
        {
            get;
            protected set;
        }

        public UInt32[] Indices
        {
            get;
            protected set;
        }

        public string ShaderName
        {
            get;
            protected set;
        }

        protected readonly BaseDevice _device;

        public BaseModelBuffer( BaseDevice device, BufferVertex[] vertices, UInt32[] indices, string shaderName = "DefaultSurface" )
        {
            _device = device;
            Vertices = vertices;
            Indices = indices;
            ShaderName = shaderName;
        }

        public virtual void Begin( )
        {
        }


        public virtual void End( )
        {
        }

        public virtual void DrawPoly( GLPoly poly, List<dlight_t> dynamicLights )
        {
        }

        public virtual void Draw( )
        {
        }

        public virtual void BeginTexture( BaseTexture texture, BaseTexture lightmapTexture, Double time, bool noLightmap, bool waveDistort, Double waveScale, float opacity )
        {
        }

        public virtual void Dispose( )
        {
            Vertices = null;
            Indices = null;
        }

        public static BaseModelBuffer New( BaseDevice device, BufferVertex[] vertices, UInt32[] indices, string shaderName = "DefaultSurface" )
        {
            return ( BaseModelBuffer ) Activator.CreateInstance( device.ModelBufferType, device, vertices, indices, shaderName );
        }
    }

    [StructLayout( LayoutKind.Sequential, Pack = 4 )]
    public struct BufferVertex
    {
        public float X;
        public float Y;
        public float Z;

        public float U;
        public float V;

        public float U2;
        public float V2;
    }
}
