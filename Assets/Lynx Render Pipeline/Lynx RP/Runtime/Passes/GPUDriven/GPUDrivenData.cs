using UnityEngine.Rendering.RenderGraphModule;

namespace LynxRP
{
    public readonly ref struct GPUDrivenData
    {
        public readonly BufferHandle indexBuffer, voteBuffer;
        public readonly BufferHandle offsetSizesBuffer, matricesBuffer, aabbBuffer;
        public readonly BufferHandle triangleBuffer, bboxBuffer, quadBuffer;
        public readonly BufferHandle vertexPassBuffer, bboxPassBuffer, quadPassBuffer;
        public readonly BufferHandle argsBuffer, argsLineBuffer, argsQuadBuffer;


        public GPUDrivenData(
            BufferHandle indexBuffer, 
            BufferHandle voteBuffer,

            BufferHandle offsetSizesBuffer, 
            BufferHandle matricesBuffer, 
            BufferHandle aabbBuffer,

            BufferHandle triangleBuffer, 
            BufferHandle bboxBuffer, 
            BufferHandle quadBuffer,

            BufferHandle vertexPassBuffer, 
            BufferHandle bboxPassBuffer, 
            BufferHandle quadPassBuffer,

            BufferHandle argsBuffer, 
            BufferHandle argsLineBuffer, 
            BufferHandle argsQuadBuffer

        )
        {
            this.indexBuffer = indexBuffer;
            this.voteBuffer = voteBuffer;

            this.offsetSizesBuffer = offsetSizesBuffer;
            this.matricesBuffer = matricesBuffer;
            this.aabbBuffer = aabbBuffer;

            this.triangleBuffer = triangleBuffer;
            this.bboxBuffer = bboxBuffer;
            this.quadBuffer = quadBuffer;

            this.vertexPassBuffer = vertexPassBuffer;
            this.bboxPassBuffer = bboxPassBuffer;
            this.quadPassBuffer = quadPassBuffer;

            this.argsBuffer = argsBuffer;
            this.argsLineBuffer = argsLineBuffer;
            this.argsQuadBuffer = argsQuadBuffer;
        }
    }
}
