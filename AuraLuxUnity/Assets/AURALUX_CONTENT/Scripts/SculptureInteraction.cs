using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Collider))]
public class SculptureInteraction : MonoBehaviour
{
    [SerializeField] private Light sculptureLight;
    [SerializeField] private ParticleSystem particleFX;
    [SerializeField] private GameObject sculptureMaterialObject;
    private Material instantiatedMaterial; // To avoid modifying the original material asset

    [SerializeField] private Vector2 glowStrengthRange = new Vector2(0.5f, 2f);
    [SerializeField] private Vector2 lightIntensityRange = new Vector2(0.5f, 2f);
    [SerializeField] private Vector2 fresnelStrengthRange = new Vector2(0.5f, 2f);
    [SerializeField] private float audioSensitivity = 8f; // Scales raw PCM amplitude (typically 0.01-0.15) to 0-1 range
    [SerializeField] private float audioVolume = 0.3f; // Toggle between basic amplitude mapping and ADSR envelope response
    [Header("ADSR Envelope")]
    [SerializeField] private float peakThreshold = 0.6f;    // Normalized amplitude that triggers Attack
    [SerializeField] private float sustainThreshold = 0.3f; // Normalized amplitude required to hold Sustain
    [SerializeField] private float attackTime = 0.05f;      // Seconds to rise from current value to 1.0
    [SerializeField] private float decayTime = 0.1f;        // Seconds to fall from 1.0 to sustainLevel
    [SerializeField] private float sustainLevel = 0.6f;     // Envelope level held during Sustain stage (0-1)
    [SerializeField] private float releaseTime = 0.4f;      // Seconds to fall from sustainLevel to 0

    private enum EnvelopeState { Idle, Attack, Decay, Sustain, Release }
    private EnvelopeState envelopeState = EnvelopeState.Idle;
    private float currentEnvelope = 0f;


    [SerializeField] private float audioFadeOutTime = 1f;

    private AudioSource audioSource;
    private bool isActive = false;
    private Coroutine fadeCoroutine;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (sculptureMaterialObject != null)
        {
            Renderer renderer = sculptureMaterialObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                instantiatedMaterial = renderer.material; // Accessing .material auto-instances it
            }
        }
 
        ResetInteraction();
    }

    // Update is called once per frame
    void Update()
    {
        //AudioReactionBasic();
        AudioReactionAdvanced();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            StartInteraction();
        }        
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            ResetInteraction();
        }
    }

    void AudioReactionAdvanced()
    {
        if (!isActive || audioSource == null || !audioSource.isPlaying)
            return;

        // Sample the audio
        float[] samples = new float[64];
        audioSource.GetOutputData(samples, 0);
        float averageSample = 0f;
        foreach (float sample in samples)
            averageSample += Mathf.Abs(sample);
        averageSample /= samples.Length;
        float normalizedSample = Mathf.Clamp01(averageSample * audioSensitivity);

        // ADSR state transitions
        switch (envelopeState)
        {
            case EnvelopeState.Idle:
                if (normalizedSample >= peakThreshold)
                    envelopeState = EnvelopeState.Attack;
                break;

            case EnvelopeState.Attack:
                currentEnvelope = Mathf.MoveTowards(currentEnvelope, 1f, Time.deltaTime / attackTime);
                if (currentEnvelope >= 1f)
                    envelopeState = EnvelopeState.Decay;
                // Re-trigger: a new peak while already in Attack just keeps us here
                break;

            case EnvelopeState.Decay:
                currentEnvelope = Mathf.MoveTowards(currentEnvelope, sustainLevel, Time.deltaTime / decayTime);
                if (normalizedSample >= peakThreshold)           // New peak — retrigger
                    envelopeState = EnvelopeState.Attack;
                else if (Mathf.Approximately(currentEnvelope, sustainLevel))
                    envelopeState = EnvelopeState.Sustain;
                break;

            case EnvelopeState.Sustain:
                if (normalizedSample >= peakThreshold)           // New peak — retrigger
                    envelopeState = EnvelopeState.Attack;
                else if (normalizedSample < sustainThreshold)    // Audio dropped off — release
                    envelopeState = EnvelopeState.Release;
                break;

            case EnvelopeState.Release:
                currentEnvelope = Mathf.MoveTowards(currentEnvelope, 0f, Time.deltaTime / releaseTime);
                if (normalizedSample >= peakThreshold)           // New peak during release — retrigger
                    envelopeState = EnvelopeState.Attack;
                else if (Mathf.Approximately(currentEnvelope, 0f))
                    envelopeState = EnvelopeState.Idle;
                break;
        }

        // Apply envelope to material and light
        float glowStrength    = Mathf.Lerp(glowStrengthRange.x,    glowStrengthRange.y,    currentEnvelope);
        float lightIntensity  = Mathf.Lerp(lightIntensityRange.x,  lightIntensityRange.y,  currentEnvelope);
        float fresnelStrength = Mathf.Lerp(fresnelStrengthRange.x, fresnelStrengthRange.y, currentEnvelope);

        if (instantiatedMaterial != null)
        {
            instantiatedMaterial.SetFloat("_GlowStrength", glowStrength);
            instantiatedMaterial.SetFloat("_FresnelStrength", fresnelStrength);
        }
        if (sculptureLight != null)
        {
            sculptureLight.intensity = lightIntensity;
        }
    }

    void StartInteraction()
    {
        isActive = true;
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
        if (audioSource != null)
        {
            audioSource.volume = audioVolume;
            if (!audioSource.isPlaying)
                audioSource.Play();
        }
        if(particleFX != null)
        {
            particleFX.Play();
        }
    }
    void ResetInteraction()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeOutAudio());
        }

        if (instantiatedMaterial != null)
        {
            instantiatedMaterial.SetFloat("_GlowStrength", glowStrengthRange.x);
            instantiatedMaterial.SetFloat("_FresnelStrength", fresnelStrengthRange.x);
        }
        if (sculptureLight != null)
        {
            sculptureLight.intensity = lightIntensityRange.x;
        }

        if(particleFX != null)
        {
            particleFX.Stop();
        }

        currentEnvelope = 0f;
        envelopeState = EnvelopeState.Idle;
        isActive = false;
    }

    System.Collections.IEnumerator FadeOutAudio()
    {
        float startVolume = audioSource.volume;
        float elapsed = 0f;
        while (elapsed < audioFadeOutTime)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / audioFadeOutTime);
            yield return null;
        }
        audioSource.Stop();
        audioSource.volume = audioVolume;
        fadeCoroutine = null;
    }
}
