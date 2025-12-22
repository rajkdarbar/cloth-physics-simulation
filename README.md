# 🧵 Real-Time Cloth Physics Simulation

This project describes a **from-scratch real-time cloth physics simulation** implemented in **Unity**, designed to explore and understand the core principles of cloth modeling, numerical integration, and collision handling in computer graphics.

---

## 🧩 Features Implemented

- Mass–spring cloth model with structural, shear, and bend constraints  
- Spring, damping, wind, drag, and gravity forces  
- Semi-implicit Euler, Verlet, and Position-Based Dynamics (PBD) integrators  
- Cloth collision with spheres, cubes, a ground plane, and arbitrary triangle meshes (PBD only)  
- Bounding Volume Hierarchy (BVH) construction from triangle meshes on the CPU  
- BVH flattening into linear arrays for efficient GPU traversal  
- GPU compute shader–based BVH traversal and cloth–mesh collision detection  
- Real-time interaction, runtime parameter tuning, and debug visualization

## 🖼️ Sample Results

#### Cloth surface and mass–spring constraint visualization

<div align="left">
  <img src="Assets/Resources/rendered-cloth.png" width="260">
  <img src="Assets/Resources/rendered-cloth-grid.png" width="270">  
</div>

#### Cloth collision with sphere, cube, and triangle mesh

<div align="left">
  <img src="Assets/Resources/cloth-sphere-collision.png" width="250">
  <img src="Assets/Resources/cloth-cube-collision-01.png" width="220">
  <img src="Assets/Resources/cloth-mesh-collision.png" width="261">
</div>

## 🎥 Demo Video
A short walkthrough video demonstrating the cloth simulation, interaction controls, and collision behavior is available **[on YouTube](https://youtu.be/Dfn9mNK74pE)**.
