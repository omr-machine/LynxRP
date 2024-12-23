using UnityEngine.Rendering.RenderGraphModule;

namespace LynxRP
{
    public readonly ref struct HiZData
    {
        public readonly TextureHandle hiZDepthRT;
        public readonly int mipLevelMax;
        public readonly TextureHandle hiZInterFrameRT;
        public readonly TextureHandle hiZPrevFrameDataRT;
        public readonly BufferHandle pointBuffer;

        public HiZData(
            TextureHandle hiZDepthRT,
            int mipLevelMax,
            TextureHandle hiZInterFrameRT,
            TextureHandle hiZPrevFrameDataRT,
            BufferHandle pointBuffer
        )
        {
            this.hiZDepthRT = hiZDepthRT;
            this.mipLevelMax = mipLevelMax;
            this.hiZInterFrameRT = hiZInterFrameRT;
            this.hiZPrevFrameDataRT = hiZPrevFrameDataRT;
            this.pointBuffer = pointBuffer;
        }
    }
}
