# WaveBlender-Sound-Transformer-Compute-Unity3D

- Sound Transformer Data Compute System 

[![Unity Version](https://img.shields.io/badge/Unity-6000.0.26%2B-blue.svg)](https://unity.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Mac%20%7C%20Linux-lightgrey.svg)]()

A high-performance, GPU-accelerated sound simulation and synthesis system for Unity, using FDTD (Finite-Difference Time-Domain) methods. This project enables realistic sound propagation, material-based effects, and interactive audio synthesis for games and research.

## Features

| Feature                | Description                                                                 |
|------------------------|-----------------------------------------------------------------------------|
| FDTD Sound Simulation  | Real-time pressure and velocity field simulation using compute shaders.      |
| Material Modeling      | Supports air, steel, aluminum, wood, and water for realistic sound effects. |
| Enhanced Sources       | Trigger metallic, wooden, and liquid sounds with physical parameters.       |
| Audio Output           | Direct synthesis to Unity's AudioSource with post-processing.               |
| Visualization          | 3D grid and source visualization in the Unity Editor.                       |
| Manual Triggering      | Interactive sound events via keyboard or script.                            |

## Demo

- Unity 6000.0.26f1
<img width="1228" height="938" alt="image" src="https://github.com/user-attachments/assets/9c0d19f3-896d-4a8e-9fae-6527d8a088b3" />


## Getting Started

1. **Clone the repository:**
   ```sh
   git clone https://github.com/InboraStudio/WaveBlender-Sound-Transformer-Compute-Unity3D.git
   ```
2. **Open in Unity (2022.3+ recommended).**
3. **Assign the `WaveBlenderSolver` and `WaveBlenderDemo` scripts to GameObjects.**
4. **Assign the `WaveBlenderFDTD.compute` shader to the solver.**
5. **Press Play and interact using Space (metal), W (wood), Q (water drop).**

## Scripts Overview

| Script                  | Purpose                                                      |
|-------------------------|--------------------------------------------------------------|
| [`WaveBlenderSolver`](Assets/Inbora Studio/Scripts/WaveBlenderSolver.cs) | Core simulation and audio synthesis.                |
| [`WaveBlenderDemo`](Assets/Inbora Studio/Scripts/WaveBlenderDemo.cs)     | Keyboard-triggered sound events.                    |

## How It Works

- **FDTD Simulation:** Pressure and velocity fields are updated on the GPU using [`WaveBlenderFDTD.compute`](Assets/Inbora Studio/Shaders/WaveBlenderFDTD.compute).
- **Material Effects:** Each sound source can be assigned a material type, affecting propagation and timbre.
- **Audio Sampling:** Listener position is mapped to the simulation grid, and audio is synthesized in real time.

## Reference

For more details on FDTD sound simulation, see the foundational paper:

- [Finite-Difference Time-Domain Simulation of Sound Propagation in 3D](https://www.researchgate.net/publication/220660883_Finite-Difference_Time-Domain_Simulation_of_Sound_Propagation_in_3D)


## Acknowledgements

- Unity Technologies
- [Newtonsoft.Json](https://www.newtonsoft.com/json)
- Research on FDTD sound simulation

---

## Technical Overview & Math Behind the Magic 

### Core Technologies

| Technology         | Purpose                                                      |
|--------------------|-------------------------------------------------------------|
| **Unity Compute Shaders** | High-performance, parallel simulation of sound fields on GPU. |
| **Finite-Difference Time-Domain (FDTD)** | Numerical method for simulating wave propagation in 3D. |
| **Material Modeling** | Realistic sound behavior for air, steel, wood, water, etc. |
| **Async GPU Readback** | Efficient transfer of simulation data from GPU to CPU for audio output. |
| **Real-Time Audio Synthesis** | Converts simulated pressure fields into playable audio in Unity. |

---

### Mathematical Foundations

#### 1. **FDTD Wave Equation**

The project solves the 3D acoustic wave equation using FDTD:

$$
\frac{\partial^2 p}{\partial t^2} = c^2 \nabla^2 p
$$

Where:
- \( p \) = pressure field
- \( c \) = speed of sound (varies by material)
- \( \nabla^2 \) = Laplacian operator (spatial derivatives)

The pressure and velocity fields are discretized on a 3D grid and updated every timestep using central differences.

#### 2. **Material-Dependent Sound Propagation**

Each grid cell can represent a different material (air, steel, wood, water, etc.), affecting:
- **Density (\( \rho \))**
- **Speed of Sound (\( c \))**
- **Damping/Absorption**

This enables realistic simulation of sound transmission, reflection, and absorption.

#### 3. **Source Modeling**

Sound sources are injected with physical parameters:
- **Frequency**
- **Amplitude**
- **Damping**
- **Material Type**
- **Phase**

Special logic in the compute shader creates metallic ringing, wooden knocks, and water drops using envelope functions and harmonics.

#### 4. **Audio Sampling & Synthesis**

The pressure field near the listener is sampled and spatially averaged for anti-aliasing. The sampled pressure is then post-processed:
- **Soft Clipping:** Prevents harsh distortion.
- **Low-Pass Filtering:** Smooths the audio signal.
- **Simple Reverb:** Adds echo using a circular buffer.

#### 5. **Visualization**

The simulation grid and sources are visualized in the Unity Editor using Gizmos, with color-coding for different materials.

---

### Cool Features

- **GPU-Accelerated Physics:** Real-time sound simulation for large 3D grids.
- **Material-Aware Audio:** Hear the difference between metal, wood, and water impacts.
- **Interactive Triggering:** Sounds can be triggered via keyboard or script.
- **Research-Grade Math:** Based on [FDTD sound propagation papers](https://graphics.stanford.edu/papers/waveblender/).

---

### References

- [Finite-Difference Time-Domain Simulation of Sound Propagation in 3D](https://www.researchgate.net/publication/220660883_Finite-Difference_Time-Domain_Simulation_of_Sound_Propagation_in_3D)
- [Unity Compute Shaders Documentation](https://docs.unity3d.com/Manual/class-ComputeShader.html)

---

*Explore the code for more details and experiment with different materials
