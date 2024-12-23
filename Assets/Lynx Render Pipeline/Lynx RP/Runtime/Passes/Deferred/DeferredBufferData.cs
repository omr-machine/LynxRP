using UnityEngine.Rendering.RenderGraphModule;

namespace LynxRP
{
    public readonly ref struct DeferredRenderTextures
    {
        public readonly TextureHandle
            positionBuffer,
            normalBuffer,
            normalInterpolatedBuffer,
            ormBuffer,
            lightingBuffer,
            extrasBuffer;
            

        public DeferredRenderTextures(
            TextureHandle positionBuffer,
            TextureHandle normalBuffer, 
            TextureHandle normalInterpolatedBuffer,
            TextureHandle ormBuffer,
            TextureHandle lightingBuffer,
            TextureHandle extrasBuffer
        )
        {
            this.positionBuffer = positionBuffer;
            this.normalBuffer = normalBuffer;
            this.normalInterpolatedBuffer = normalInterpolatedBuffer;
            this.ormBuffer = ormBuffer;
            this.lightingBuffer = lightingBuffer;
            this.extrasBuffer = extrasBuffer;
        }
    }
}
