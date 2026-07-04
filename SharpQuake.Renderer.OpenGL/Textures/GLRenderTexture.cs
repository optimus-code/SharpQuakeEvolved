using SharpQuake.Renderer.Textures;

namespace SharpQuake.Renderer.OpenGL.Textures
{
    public class GLRenderTexture : IRenderTexture
    {
        public int ID
        {
            get;
            private set;
        }


        public int Width
        {
            get;
            private set;
        }

        public int Height
        {
            get;
            private set;
        }

        public GLRenderTexture( int id, int width, int height )
        {
            ID = id;
            Width = width;
            Height = height;
        }
    }
}
