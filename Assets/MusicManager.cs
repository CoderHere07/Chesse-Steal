using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    private static MusicManager _instance;
    public static MusicManager Instance => _instance;

    private AudioSource _audioSource;
    private AudioSource _dangerAudioSource;

    [Tooltip("Short danger/heartbeat clip — place in Assets/Resources/ and enter its name without extension.")]
    public string dangerClipName = "danger_pulse";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        // Check if there is already a MusicManager in the scene
        if (FindObjectOfType<MusicManager>() == null)
        {
            GameObject go = new GameObject("MusicManager");
            go.AddComponent<MusicManager>();
        }
    }

    private void Awake()
    {
        // Maintain a single instance in the scene
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // Persist across scene loads so music continues continuously on restart
        DontDestroyOnLoad(gameObject);

        // Add AudioSource for background music
        _audioSource = gameObject.AddComponent<AudioSource>();

        // Load the background music track from the Resources directory
        AudioClip clip = Resources.Load<AudioClip>("makanghubert-tom-amp-jerry-type-417839");
        if (clip != null)
        {
            _audioSource.clip = clip;
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
            // _audioSource.Play(); // Disabled per user request
        }
        else
        {
            Debug.LogWarning("MusicManager: Background music clip 'makanghubert-tom-amp-jerry-type-417839' not found in Resources folder!");
        }

        // Add AudioSource for danger SFX (separate so it doesn't interrupt music)
        _dangerAudioSource = gameObject.AddComponent<AudioSource>();
        _dangerAudioSource.loop        = false;
        _dangerAudioSource.playOnAwake = false;
        _dangerAudioSource.volume      = 0.6f;

        // Register for scene loaded event to play music if it was stopped (e.g. after win)
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // If the music was stopped (e.g., from a win state), start playing it again
        if (_audioSource != null && !_audioSource.isPlaying)
        {
            // _audioSource.Play(); // Disabled per user request
        }
    }

    /// <summary>
    /// Stops the music playback.
    /// </summary>
    public void StopMusic()
    {
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
    }

    /// <summary>
    /// Plays a short danger pulse SFX. Called by TrapProximityDetector.
    /// If no clip is found in Resources, generates a simple beep via AudioClip.
    /// </summary>
    public void PlayDangerPulse()
    {
        if (_dangerAudioSource == null) return;
        if (_dangerAudioSource.isPlaying) return;  // don't stack

        AudioClip dangerClip = Resources.Load<AudioClip>(dangerClipName);
        if (dangerClip != null)
        {
            _dangerAudioSource.PlayOneShot(dangerClip);
        }
        else
        {
            // Procedural fallback: short sine-wave beep
            _dangerAudioSource.PlayOneShot(GenerateBeep(440f, 0.15f));
        }
    }

    /// <summary>
    /// Generates a short sine-wave audio clip programmatically.
    /// Used as a fallback when no danger SFX file is provided.
    /// </summary>
    private static AudioClip GenerateBeep(float frequency, float duration)
    {
        int sampleRate  = 44100;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - (t / duration);   // simple linear fade-out
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.5f;
        }

        AudioClip clip = AudioClip.Create("DangerBeep", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
