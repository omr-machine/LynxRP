![LOGO](LynxLogo.png "a title")

# Lynx RP

A Unity SRP (Scriptable Render Pipeline) targeting Desktop (Windows, Linux), Web (WebGPU), and Console; with a focus on Simplicity, NPR, and Art Directability.

Originally this RP was based on the tutorial by [Catlike Coding](https://catlikecoding.com/unity/tutorials/custom-srp/), which I was following to learn about Rendering and Graphics. I have been adding my own features since then (ex Deferred Rendering, GPU Culling, etc). Wanting to showcase the work I have done, and have it potentially help others learn about SRPs, I decided to share the project online.

## Features
- Forward Tiled
- Deferred Tiled
- GPU Driven (WIP)
- Simple Lit (Cook-Torrance)
- Transparent Lit
- Shadow Maps, Cascade Shadow Maps
    * Point Light Shadows
    * Spot Light Shadows
    * Directional Light Shadow
- Light Baking with Unity
- FXAA
- MSAA (With Forward Rendering)
- HDR
- Post Processing
    * Bloom
    * Tonemapping
    * Color Grading
- Geometry Pass Culling, Compute Culling (With GPU Driven)
    - HiZ Culling
    - Frustum Culling
    - Small Primitive Culling
    - Orientation Culling

## Planned
Read TODO.md.

## Known Issues
- Deferred Rendering Pass is black.
    * This is fixed by going in the particles scene and clicking on the objects in the scene or the camera, or enabling the frame debugger.
    * I think it is caused by ther per object data not being initialized for the pass.
- Switching Pipeline path breaks Render Graph Viewer
    * I don't know a fix for this. Seems to be you have to restart the project to get the Viewer working again.
    * I am not sure what causes this, but it might be related to how the render graph gets disposed?
- HiZ Culling broken on Forward and Deferred
    * Switch to GPU Driven path and make sure HiZ Culling is enabled. Now it should work when you switch back to Forward/Deferred.
    * Not sure what is causing this.

## References 
Read REFERENCES.md.
