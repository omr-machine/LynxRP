# REFERENCES
## Graphics
- GPU Programming course from Georgia Tech https://www.youtube.com/watch?v=i5yK56XFbrU&list=PLOunECWxELQQwayE8e3WjKPJsTGKknJ8w
- Trip through the graphics pipeline https://fgiesen.wordpress.com/2011/07/09/a-trip-through-the-graphics-pipeline-2011-index/

## Culling
- AAA culling on consoles https://ubm-twvideo01.s3.amazonaws.com/o1/vault/gdc2016/Presentations/Wihlidal_Graham_OptimizingTheGraphics.pdf
- Assassins Creed culling https://advances.realtimerendering.com/s2015/aaltonenhaar_siggraph2015_combined_final_footer_220dpi.pdf

- GPU HiZ culling and prefix sum https://interplayoflight.wordpress.com/2017/11/15/experiments-in-gpu-based-occlusion-culling/

- Unity Compute Culling
  * https://github.com/xukunn1226/Indirect-Rendering-With-Compute-Shaders
  * https://github.com/CrazyEngine/Unity_Indirect-Rendering-With-Compute-Shaders
  * https://github.com/Milk-Drinker01/Milk_Instancer01

- Grass Instancer https://github.com/MangoButtermilch/Unity-Grass-Instancer

### Frustum Culling
- Inigo Quilez Frustum Culling https://iquilezles.org/articles/frustumcorrect/
- Frustm Culling OBB https://bruop.github.io/frustum_culling/
- Vulkan Guide Compute Culling https://vkguide.dev/docs/gpudriven/compute_culling/
- Frustum Corners Extraction https://donw.io/post/frustum-point-extraction/
- Unity Discussion Compute Culling https://discussions.unity.com/t/frustum-culling-with-compute-shader-how/941164/12
- Frustum Plane Extraction Gist https://gist.github.com/podgorskiy/e698d18879588ada9014768e3e82a644

- Graphics Space Transformations https://www.cs.utexas.edu/~fussell/courses/cs354-fall2015/lectures/lecture9.pdf

### HiZ Culling 
- HiZ Culling Non Power of 2 https://miketuritzin.com/post/hierarchical-depth-buffers/
- Unity HiZ Grass Culling https://github.com/jackie2009/HiZ_grass_culling

- "There is an alternative form that we will likely implement at some point that removes all pops at 
      the cost of a bit of extra book-keeping and complexity that doesn't use previous-frame depth at all. 
   You instead prime the depth buffer with all previously-visible geometry 
      (e.g. everything that passed occlusion the previous frame) and use that buffer for occlusion-culling. 
   This does require a fully gpu-driven renderer though.
   draw what was visible last frame in your z-prepass
   cull what wasn't visible last frame against your partial z-prepass
   draw whatever passed (2) into your z-prepass, now its complete
   do forward shading & ballot/record what was truly visible this frame"

## Prefix sum
- Acerola prefix sum https://github.com/GarrettGunnell/Grass/tree/main
- GPU Gems prefix sum https://developer.nvidia.com/gpugems/gpugems3/part-vi-gpu-computing/chapter-39-parallel-prefix-sum-scan-cuda
- Lecture GPU Parallel scans https://people.cs.pitt.edu/~bmills/docs/teaching/cs1645/lecture_gpu_algo.pdf
- Nvidia Parallel Reduction Optimization https://developer.download.nvidia.com/compute/cuda/1.1-Beta/x86_website/projects/reduction/doc/reduction.pdf

- New Prefix Sum Algorithm https://research.nvidia.com/sites/default/files/pubs/2016-03_Single-pass-Parallel-Prefix/nvr-2016-002.pdf
- Vulkan Prefix Sum https://raphlinus.github.io/gpu/2020/04/30/prefix-sum.html
- Multiple Prefix Sum examples https://github.com/b0nes164/GPUPrefixSums

- https://www.youtube.com/watch?v=DrD3eIw74RY&t=969s

## Anti-Aliasing
- Analitical AA and some AA Resources https://blog.frost.kiwi/analytical-anti-aliasing/

### CMAA2
- https://www.intel.com/content/www/us/en/developer/articles/technical/conservative-morphological-anti-aliasing-20.html

### MSAA
- MSAA Compute Resolve https://wickedengine.net/2016/11/how-to-resolve-an-msaa-depthbuffer/comment-page-1/
- MSAA Passes for Deferred https://therealmjp.github.io/posts/deferred-msaa/

## Blur
- Dual Kawase Blur

## Shadows

### Shadow Maps
- Irregular Shadow mapping https://mid.net.ua/posts/izb.html
- Rendering Fake Soft Shadows with Smoothies https://people.csail.mit.edu/ericchan/papers/smoothie/smoothie.pdf
- Exponential Shadow Maps https://jankautz.com/publications/esm_gi08.pdf

### Screenspace Shadows
- Screenspace Shadows https://panoskarabelas.com/posts/screen_space_shadows/

## Lighting
- Area Lights Linearly Transformed Cosines https://eheitzresearch.wordpress.com/415-2/

## Forward+
- Infinite Warfare Z Binning https://advances.realtimerendering.com/s2017/2017_Sig_Improved_Culling_final.pdf

## Visibility Rendering
- http://filmicworlds.com/blog/visibility-buffer-rendering-with-material-graphs/

## Software Rasterisation
- Triangle Interpolation https://codeplea.com/triangular-interpolation

## Unity
### Unity SRP
- CustomSRP Examples https://github.com/cinight/CustomSRP/tree/master
- ToonRP https://github.com/Delt06/toon-rp
- AAA RP https://github.com/Delt06/aaaa-rp/

### Unity Mesh API
- Read/Write Mesh Data using Jobs https://github.com/Unity-Technologies/MeshApiExamples/blob/master/Assets/CreateMeshFromAllSceneMeshes/CreateMeshFromWholeScene.cs

