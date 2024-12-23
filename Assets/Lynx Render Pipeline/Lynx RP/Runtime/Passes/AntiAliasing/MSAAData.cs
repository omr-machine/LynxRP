using UnityEngine.Rendering.RenderGraphModule;

namespace LynxRP
{
    public class MSAAData
    {
        
    }
    public readonly ref struct MSAARenderTextures
    { 
        public readonly TextureHandle 
            msaaStencil,
            msaaFill, msaaFillDepth,
            msaaBlur, msaaBlurDepth,
            msaaDownscale, msaaDownscaleDepth;

        public MSAARenderTextures(
            TextureHandle msaaStencil, 
            TextureHandle msaaFill,
            TextureHandle msaaFillDepth,
            TextureHandle msaaBlur,
            TextureHandle msaaBlurDepth,
            TextureHandle msaaDownscale,
            TextureHandle msaaDownscaleDepth
        )
        {
            this.msaaStencil = msaaStencil;
            this.msaaFill = msaaFill;
            this.msaaFillDepth = msaaFillDepth;
            this.msaaBlur = msaaBlur;
            this.msaaBlurDepth = msaaBlurDepth;
            this.msaaDownscale = msaaDownscale;
            this.msaaDownscaleDepth = msaaDownscaleDepth;
        }
    }    
}
