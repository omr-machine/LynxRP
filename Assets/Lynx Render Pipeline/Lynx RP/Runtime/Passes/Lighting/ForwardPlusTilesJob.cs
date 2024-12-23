using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Mathematics.math;

namespace LynxRP
{
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public struct ForwardPlusTilesJob : IJobFor
    {
        [ReadOnly]
        public NativeArray<float4> lightBounds;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<int> tileData;

        public int otherLightCount;

        public float2 tileScreenUVSize;

        public int maxLightsPerTile;

        public int tilesPerRow;

        public int tileDataSize;

        public void Execute(int tileIndex)
        {
            int y = tileIndex / tilesPerRow;
            int x = tileIndex - y * tilesPerRow;
            var bounds = float4(x, y, x + 1, y + 1) * tileScreenUVSize.xyxy;

            int headerIndex = tileIndex * tileDataSize;
            int dataIndex = headerIndex;
            int lightsInTileCount = 0;

            for (int i = 0; i < otherLightCount; i++)
            {
                float4 b = lightBounds[i];
                if (all(float4(b.xy, bounds.xy) <= float4(bounds.zw, b.zw)))
                {
                    tileData[++dataIndex] = i;
                    if (++lightsInTileCount >= maxLightsPerTile)
                    {
                        break;
                    }
                }
            }
            tileData[headerIndex] = lightsInTileCount;
        }
    }
}
