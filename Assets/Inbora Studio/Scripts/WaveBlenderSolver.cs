using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine.Rendering;

[System.Serializable]
public struct PointSource
{
    public Vector3 position;
    public float amplitude;
    public float frequency;
}

[System.Serializable]
public struct EnhancedPointSource
{
    public Vector3 position;
    public float amplitude;
    public float frequency;
    public float damping;
    public int materialType; // 0=air, 1=steel, 2=aluminum, 3=wood, 4=water
    public bool autoTrigger;
    public float triggerInterval;
}

public class WaveBlenderSolver : MonoBehaviour
{
    [Header("Simulation Parameters")]
    public Vector3Int gridDimensions = new Vector3Int(64, 64, 64);
    public float gridSpacing = 0.02f; // 2cm spacing
    public float timeStep = 0.00002f; // 20 microseconds for stability
    public float speedOfSound = 343.0f;
    public float airDensity = 1.225f;
    
    [Header("Audio Output")]
    public AudioSource audioSource;
    [Range(0.001f, 1.0f)]
    public float audioGain = 0.1f;
    
    [Header("Sound Sources")]
    public List<PointSource> pointSources = new List<PointSource>();
    
    [Header("Enhanced Sound Sources")]
    public List<EnhancedPointSource> enhancedSources = new List<EnhancedPointSource>();
    
    [Header("Audio Post-Processing")]
    [Range(0.0f, 1.0f)]
    public float lowPassCutoff = 0.8f;
    [Range(0.0f, 1.0f)]
    public float reverbAmount = 0.2f;
    public bool enableSmoothing = true;
    
    [Header("Visualization")]
    public bool enableVisualization = true;
    public bool showPressureField = false;
    
    // Compute shader and resources
    public ComputeShader fdtdShader;
    private RenderTexture pressureField;
    private RenderTexture velocityField;
    private RenderTexture betaField;
    private ComputeBuffer sourceBuffer;
    private ComputeBuffer audioBuffer;
    private ComputeBuffer sourcePropertiesBuffer;
    
    // Kernel IDs
    private int updatePressureKernel;
    private int updateVelocityKernel; 
    private int updateBetaKernel;
    private int sampleAudioKernel;
    
    // Audio system
    private const int AUDIO_BUFFER_SIZE = 512;
    private float[] audioData;
    private int audioSampleRate = 44100;
    private float simulationTime = 0f;
    private Vector3 listenerPosition;
    
    // Audio processing (thread-safe)
    private volatile float[] audioHistory;
    private const int HISTORY_SIZE = 64;
    private float lastTriggerTime = 0f;
    private int audioSampleCounter = 0;
    
    // GPU readback
    private NativeArray<float> audioReadbackData;
    private bool audioReadbackPending = false;
    
    void Start()
    {
        // Initialize audio history for smoothing
        audioHistory = new float[HISTORY_SIZE];
        
        // Add default enhanced sources if none exist
        if (enhancedSources.Count == 0)
        {
            // Metallic impact sound
            enhancedSources.Add(new EnhancedPointSource
            {
                position = new Vector3(0.4f, 0.5f, 0.5f),
                amplitude = 15.0f,
                frequency = 800.0f,
                damping = 2.0f,
                materialType = 1, // Steel
                autoTrigger = true,
                triggerInterval = 2.0f
            });
            
            // Wood knock sound
            enhancedSources.Add(new EnhancedPointSource
            {
                position = new Vector3(0.6f, 0.5f, 0.5f),
                amplitude = 12.0f,
                frequency = 400.0f,
                damping = 4.0f,
                materialType = 3, // Wood
                autoTrigger = true,
                triggerInterval = 3.0f
            });
        }
        
        InitializeSimulation();
        SetupAudio();
        
        Debug.Log("WaveBlender initialized successfully!");
    }
    
    void InitializeSimulation()
    {
        if (fdtdShader == null)
        {
            Debug.LogError("WaveBlender: FDTD Compute Shader not assigned!");
            return;
        }
        
        // Find all kernels
        updatePressureKernel = fdtdShader.FindKernel("UpdatePressure");
        updateVelocityKernel = fdtdShader.FindKernel("UpdateVelocity");
        updateBetaKernel = fdtdShader.FindKernel("UpdateBeta");
        sampleAudioKernel = fdtdShader.FindKernel("SampleAudio");
        
        // Validate kernels
        if (updatePressureKernel == -1 || updateVelocityKernel == -1 || 
            updateBetaKernel == -1 || sampleAudioKernel == -1)
        {
            Debug.LogError("WaveBlender: Failed to find compute shader kernels!");
            return;
        }
        
        // Create 3D render textures
        pressureField = CreateRenderTexture3D(gridDimensions, RenderTextureFormat.RFloat);
        velocityField = CreateRenderTexture3D(gridDimensions, RenderTextureFormat.ARGBFloat);
        betaField = CreateRenderTexture3D(gridDimensions, RenderTextureFormat.RFloat);
        
        // Clear initial fields
        ClearRenderTexture3D(pressureField);
        ClearRenderTexture3D(velocityField);
        ClearRenderTexture3D(betaField);
        
        // Set global parameters
        SetShaderParameters();
        
        // Bind textures
        BindTexturesToKernels();
        
        // Setup buffers
        SetupBuffers();
        
        Debug.Log($"WaveBlender grid: {gridDimensions.x}×{gridDimensions.y}×{gridDimensions.z}, dt={timeStep}s");
    }
    
    RenderTexture CreateRenderTexture3D(Vector3Int dimensions, RenderTextureFormat format)
    {
        RenderTexture rt = new RenderTexture(dimensions.x, dimensions.y, 0, format);
        rt.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        rt.volumeDepth = dimensions.z;
        rt.enableRandomWrite = true;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.filterMode = FilterMode.Point;
        
        if (!rt.Create())
        {
            Debug.LogError($"Failed to create 3D texture: {dimensions}");
            return null;
        }
        
        return rt;
    }
    
    void ClearRenderTexture3D(RenderTexture rt)
    {
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = null;
    }
    
    void SetShaderParameters()
    {
        fdtdShader.SetInts("gridSize", gridDimensions.x, gridDimensions.y, gridDimensions.z);
        fdtdShader.SetFloat("deltaT", timeStep);
        fdtdShader.SetFloat("deltaX", gridSpacing);
        fdtdShader.SetFloat("speedOfSound", speedOfSound);
        fdtdShader.SetFloat("airDensity", airDensity);
    }
    
    void BindTexturesToKernels()
    {
        // Bind to simulation kernels
        fdtdShader.SetTexture(updatePressureKernel, "pressureField", pressureField);
        fdtdShader.SetTexture(updatePressureKernel, "velocityField", velocityField);
        fdtdShader.SetTexture(updatePressureKernel, "betaField", betaField);
        
        fdtdShader.SetTexture(updateVelocityKernel, "pressureField", pressureField);
        fdtdShader.SetTexture(updateVelocityKernel, "velocityField", velocityField);
        fdtdShader.SetTexture(updateVelocityKernel, "betaField", betaField);
        
        fdtdShader.SetTexture(updateBetaKernel, "betaField", betaField);
        
        // Bind to audio sampling kernel
        fdtdShader.SetTexture(sampleAudioKernel, "pressureField", pressureField);
    }
    
    void SetupBuffers()
    {
        // Enhanced source buffer setup
        int sourceCount = Mathf.Max(enhancedSources.Count, 1);
        sourceBuffer = new ComputeBuffer(sourceCount, sizeof(float) * 4);
        sourcePropertiesBuffer = new ComputeBuffer(sourceCount, sizeof(float) * 4);
        
        UpdateEnhancedSourceBuffers();
        
        // Audio buffer
        audioBuffer = new ComputeBuffer(AUDIO_BUFFER_SIZE, sizeof(float));
        fdtdShader.SetBuffer(sampleAudioKernel, "audioSamples", audioBuffer);
        fdtdShader.SetInt("numAudioSamples", AUDIO_BUFFER_SIZE);
        
        // Initialize audio data array
        audioData = new float[AUDIO_BUFFER_SIZE];
        audioReadbackData = new NativeArray<float>(AUDIO_BUFFER_SIZE, Allocator.Persistent);
    }
    
    void UpdateEnhancedSourceBuffers()
    {
        Vector4[] sourceData = new Vector4[sourceBuffer.count];
        Vector4[] propertiesData = new Vector4[sourcePropertiesBuffer.count];
        
        for (int i = 0; i < enhancedSources.Count && i < sourceData.Length; i++)
        {
            var source = enhancedSources[i];
            
            sourceData[i] = new Vector4(
                source.position.x,
                source.position.y,
                source.position.z,
                source.amplitude
            );
            
            // Auto-trigger logic
            float phase = 0f;
            if (source.autoTrigger && simulationTime - lastTriggerTime > source.triggerInterval)
            {
                phase = 0f; // Reset phase for new trigger
                if (i == 0) lastTriggerTime = simulationTime; // Update trigger time
            }
            else
            {
                phase = simulationTime; // Continuous phase
            }
            
            propertiesData[i] = new Vector4(
                source.frequency,
                source.damping,
                source.materialType,
                phase
            );
        }
        
        sourceBuffer.SetData(sourceData);
        sourcePropertiesBuffer.SetData(propertiesData);
        
        fdtdShader.SetBuffer(updatePressureKernel, "pointSources", sourceBuffer);
        fdtdShader.SetBuffer(updatePressureKernel, "sourceProperties", sourcePropertiesBuffer);
        fdtdShader.SetInt("numSources", enhancedSources.Count);
    }
    
    void SetupAudio()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        audioSampleRate = AudioSettings.outputSampleRate;
        audioSource.clip = AudioClip.Create("WaveBlender", audioSampleRate, 1, audioSampleRate, true, OnAudioRead);
        audioSource.loop = true;
        audioSource.volume = audioGain;
        audioSource.Play();
        
        Debug.Log($"Audio setup: {audioSampleRate}Hz, buffer size: {AUDIO_BUFFER_SIZE}");
    }
    
    void Update()
    {
        simulationTime += Time.deltaTime;
        
        // Update listener position
        if (Camera.main != null)
        {
            Vector3 worldPos = Camera.main.transform.position;
            listenerPosition = WorldToGrid(worldPos);
            fdtdShader.SetVector("listenerPosition", new Vector4(listenerPosition.x, listenerPosition.y, listenerPosition.z, 0));
        }
        
        // Update simulation time
        fdtdShader.SetFloat("currentTime", simulationTime);
        
        // Update enhanced source buffers
        UpdateEnhancedSourceBuffers();
        
        // Run FDTD simulation
        RunSimulationStep();
        
        // Sample audio for next buffer (every few frames to reduce GPU load)
        if (Time.frameCount % 2 == 0 && !audioReadbackPending)
        {
            SampleAudioFromGPU();
        }
    }
    
    Vector3 WorldToGrid(Vector3 worldPos)
    {
        Vector3 gridPos = worldPos / gridSpacing;
        gridPos.x = Mathf.Clamp(gridPos.x, 0, gridDimensions.x - 1);
        gridPos.y = Mathf.Clamp(gridPos.y, 0, gridDimensions.y - 1);
        gridPos.z = Mathf.Clamp(gridPos.z, 0, gridDimensions.z - 1);
        return gridPos;
    }
    
    void RunSimulationStep()
    {
        if (fdtdShader == null) return;
        
        int groupsX = Mathf.CeilToInt(gridDimensions.x / 8.0f);
        int groupsY = Mathf.CeilToInt(gridDimensions.y / 8.0f);
        int groupsZ = Mathf.CeilToInt(gridDimensions.z / 8.0f);
        
        // Update beta field (moving boundaries)
        fdtdShader.Dispatch(updateBetaKernel, groupsX, groupsY, groupsZ);
        
        // Update pressure and velocity fields
        fdtdShader.Dispatch(updatePressureKernel, groupsX, groupsY, groupsZ);
        fdtdShader.Dispatch(updateVelocityKernel, groupsX, groupsY, groupsZ);
    }
    
    void SampleAudioFromGPU()
    {
        if (audioBuffer == null) return;
        
        // Dispatch audio sampling kernel
        int audioGroups = Mathf.CeilToInt(AUDIO_BUFFER_SIZE / 64.0f);
        fdtdShader.Dispatch(sampleAudioKernel, audioGroups, 1, 1);
        
        // Request async GPU readback
        var request = AsyncGPUReadback.Request(audioBuffer);
        audioReadbackPending = true;
        
        // Handle readback completion
        request.WaitForCompletion();
        if (request.hasError)
        {
            Debug.LogError("GPU readback failed!");
            audioReadbackPending = false;
            return;
        }
        
        // Copy GPU data to CPU
        var gpuData = request.GetData<float>();
        for (int i = 0; i < AUDIO_BUFFER_SIZE && i < gpuData.Length; i++)
        {
            audioData[i] = gpuData[i] * audioGain;
        }
        
        audioReadbackPending = false;
    }
    
    void OnAudioRead(float[] data)
    {
        // Enhanced audio processing with smoothing and filtering (thread-safe)
        for (int i = 0; i < data.Length; i++)
        {
            float sample = 0f;
            
            if (i < audioData.Length)
            {
                sample = audioData[i];
                
                // Apply smoothing filter
                if (enableSmoothing && i > 0)
                {
                    sample = Mathf.Lerp(data[i-1], sample, lowPassCutoff);
                }
                
                // Simple reverb effect (thread-safe)
                if (reverbAmount > 0f && audioHistory != null)
                {
                    int historyIndex = (audioSampleCounter + i) % HISTORY_SIZE;
                    float reverbSample = audioHistory[historyIndex] * reverbAmount * 0.3f;
                    sample += reverbSample;
                    audioHistory[historyIndex] = sample;
                }
            }
            
            data[i] = sample * audioGain;
        }
        
        // Update counter (thread-safe)
        audioSampleCounter += data.Length;
        if (audioSampleCounter > 1000000) audioSampleCounter = 0; // Prevent overflow
    }
    
    // Add method to trigger sounds manually
    public void TriggerSound(int sourceIndex, float amplitude = -1f)
    {
        if (sourceIndex >= 0 && sourceIndex < enhancedSources.Count)
        {
            var source = enhancedSources[sourceIndex];
            if (amplitude > 0) source.amplitude = amplitude;
            source.autoTrigger = false; // Override auto-trigger
            enhancedSources[sourceIndex] = source;
            
            Debug.Log($"Triggered sound: {source.materialType} at amplitude {source.amplitude}");
        }
    }
    
    void OnDestroy()
    {
        // Clean up all resources
        if (pressureField != null) { pressureField.Release(); pressureField = null; }
        if (velocityField != null) { velocityField.Release(); velocityField = null; }
        if (betaField != null) { betaField.Release(); betaField = null; }
        if (sourceBuffer != null) { sourceBuffer.Release(); sourceBuffer = null; }
        if (audioBuffer != null) { audioBuffer.Release(); audioBuffer = null; }
        if (sourcePropertiesBuffer != null) { sourcePropertiesBuffer.Release(); sourcePropertiesBuffer = null; }
        
        if (audioReadbackData.IsCreated)
        {
            audioReadbackData.Dispose();
        }
    }
    
    void OnDrawGizmos()
    {
        if (!enableVisualization) return;
        
        // Draw simulation grid
        Gizmos.color = Color.yellow;
        Vector3 gridSize = new Vector3(
            gridDimensions.x * gridSpacing,
            gridDimensions.y * gridSpacing,
            gridDimensions.z * gridSpacing
        );
        Gizmos.DrawWireCube(transform.position + gridSize * 0.5f, gridSize);
        
        // Draw enhanced point sources
        foreach (var source in enhancedSources)
        {
            // Color based on material type
            if (source.materialType == 1) Gizmos.color = Color.gray; // Steel
            else if (source.materialType == 3) Gizmos.color = new Color(0.6f, 0.3f, 0.1f); // Wood
            else if (source.materialType == 4) Gizmos.color = Color.blue; // Water
            else Gizmos.color = Color.white; // Air
            
            Gizmos.DrawWireSphere(source.position, 0.05f);
            Gizmos.DrawLine(source.position, source.position + Vector3.up * source.amplitude * 0.01f);
        }
        
        // Draw listener
        if (Camera.main != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(Camera.main.transform.position, 0.03f);
        }
    }
}
