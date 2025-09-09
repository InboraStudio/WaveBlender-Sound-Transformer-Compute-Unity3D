using UnityEngine;

public class WaveBlenderDemo : MonoBehaviour
{
    public WaveBlenderSolver waveBlender;
    public KeyCode metalHitKey = KeyCode.Space;
    public KeyCode woodKnockKey = KeyCode.W;
    public KeyCode waterDropKey = KeyCode.Q;
    
    void Update()
    {
        if (waveBlender == null) return;
        
        // Manual sound triggering
        if (Input.GetKeyDown(metalHitKey))
        {
            waveBlender.TriggerSound(0, 20f); // Metal hit
        }
        
        if (Input.GetKeyDown(woodKnockKey))
        {
            waveBlender.TriggerSound(1, 15f); // Wood knock
        }
        
        if (Input.GetKeyDown(waterDropKey))
        {
            // Add water drop sound
            TriggerWaterDrop();
        }
    }
    
    void TriggerWaterDrop()
    {
        // Create temporary water drop sound
        var sources = waveBlender.enhancedSources;
        if (sources.Count > 2)
        {
            var waterSource = sources[2];
            waterSource.materialType = 4; // Water
            waterSource.frequency = 200f;
            waterSource.damping = 1.5f;
            waterSource.amplitude = 10f;
            sources[2] = waterSource;
        }
        
        waveBlender.TriggerSound(2, 10f);
    }
}
