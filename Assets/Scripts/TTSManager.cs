using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class TTSManager : MonoBehaviour
{
    [Header("Configuración de Voz")]
    [SerializeField] private string voiceName = "Microsoft Helena Desktop"; // Voz en español
    [SerializeField] private int voiceRate = 0; // Velocidad de habla (-10 a 10)
    [SerializeField] private int voiceVolume = 100; // Volumen (0 a 100)
    [SerializeField] private bool debugLog = true; // Mostrar mensajes de depuración

    private AudioSource audioSource;
    private bool isSpeaking = false;
    private Queue<string> textQueue = new Queue<string>();

    private static TTSManager _instance;
    public static TTSManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("TTSManager");
                _instance = go.AddComponent<TTSManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Crear AudioSource para reproducir el habla
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    /// <summary>
    /// Reproduce un texto mediante síntesis de voz
    /// </summary>
    public void Speak(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (debugLog)
        {
            Debug.Log($"[TTS] {text}");
        }

        // Encolar el texto
        textQueue.Enqueue(text);

        // Si no está hablando actualmente, iniciar el proceso
        if (!isSpeaking)
        {
            StartCoroutine(ProcessSpeechQueue());
        }
    }

    private IEnumerator ProcessSpeechQueue()
    {
        isSpeaking = true;

        while (textQueue.Count > 0)
        {
            string textToSpeak = textQueue.Dequeue();

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // Usar SAPI en Windows mediante un archivo VBS temporal
            yield return StartCoroutine(SpeakWindowsSAPI(textToSpeak));
#elif UNITY_WEBGL
            // Para WebGL usaríamos JavaScript
            yield return StartCoroutine(SpeakWebGL(textToSpeak));
#elif UNITY_ANDROID
            // Para Android usaríamos la API nativa de Android
            yield return StartCoroutine(SpeakAndroid(textToSpeak));
#elif UNITY_IOS
            // Para iOS usaríamos la API nativa de iOS
            yield return StartCoroutine(SpeakIOS(textToSpeak));
#else
            // Para otras plataformas, solo esperamos un tiempo simulado
            Debug.LogWarning("TTS no implementado para esta plataforma");
            float simulatedDuration = Mathf.Max(1.0f, textToSpeak.Length * 0.05f); // ~50ms por carácter
            yield return new WaitForSeconds(simulatedDuration);
#endif

            // Pequeña pausa entre textos
            yield return new WaitForSeconds(0.3f);
        }

        isSpeaking = false;
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private IEnumerator SpeakWindowsSAPI(string text)
    {
        bool errorOccurred = false;
        float estimatedDuration = Mathf.Max(1.0f, text.Length * 0.08f); // ~80ms por carácter

        // Usar bloque try-catch fuera del yield return
        try
        {
            // Escapar caracteres especiales y comillas
            text = text.Replace("\"", "");

            // Crear un archivo VBS temporal
            string tempPath = Path.Combine(Application.temporaryCachePath, "speech.vbs");

            using (StreamWriter writer = new StreamWriter(tempPath, false))
            {
                writer.WriteLine("Dim speaks, speech");
                writer.WriteLine($"speaks=\"{text}\"");
                writer.WriteLine("Set speech=CreateObject(\"SAPI.SpVoice\")");
                writer.WriteLine($"speech.Volume = {voiceVolume}");
                writer.WriteLine($"speech.Rate = {voiceRate}");

                // Seleccionar voz específica si se ha configurado
                if (!string.IsNullOrEmpty(voiceName))
                {
                    writer.WriteLine("For Each v in speech.GetVoices");
                    writer.WriteLine($"  If InStr(v.GetDescription, \"{voiceName}\") > 0 Then");
                    writer.WriteLine("    Set speech.Voice = v");
                    writer.WriteLine("    Exit For");
                    writer.WriteLine("  End If");
                    writer.WriteLine("Next");
                }

                writer.WriteLine("speech.Speak speaks");
                writer.WriteLine("While speech.Status.RunningState <> 1"); // 1 = SpeechRunState.SRSEDone
                writer.WriteLine("  WScript.Sleep 100");
                writer.WriteLine("Wend");
            }

            // Ejecutar el script VBS
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.FileName = tempPath;
            process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            process.Start();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error al generar o ejecutar el TTS: {ex.Message}");
            errorOccurred = true;
        }

        // Si hay error, usar tiempo estimado
        if (errorOccurred)
        {
            yield return new WaitForSeconds(estimatedDuration);
        }
        else
        {
            // Usar tiempo estimado para esperar a que termine de hablar
            yield return new WaitForSeconds(estimatedDuration);
        }
    }
#endif

#if UNITY_WEBGL
    private IEnumerator SpeakWebGL(string text)
    {
        // Esto requiere implementación JavaScript adicional
        Debug.Log("WebGL TTS requiere un plugin JavaScript");
        
        // Estimar el tiempo que tomará hablar
        float estimatedDuration = Mathf.Max(1.0f, text.Length * 0.08f);
        yield return new WaitForSeconds(estimatedDuration);
    }
#endif

#if UNITY_ANDROID
    private IEnumerator SpeakAndroid(string text)
    {
        // Esta implementación requiere código nativo de Android
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject ttsPlugin = new AndroidJavaObject("com.example.unitytts.TTSPlugin", activity))
            {
                ttsPlugin.Call("speak", text);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error al usar TTS de Android: {ex.Message}");
        }
        
        // Estimar el tiempo que tomará hablar
        float estimatedDuration = Mathf.Max(1.0f, text.Length * 0.08f);
        yield return new WaitForSeconds(estimatedDuration);
    }
#endif

#if UNITY_IOS
    private IEnumerator SpeakIOS(string text)
    {
        // Esta implementación requiere código nativo de iOS
        Debug.Log("iOS TTS no implementado");
        
        // Estimar el tiempo que tomará hablar
        float estimatedDuration = Mathf.Max(1.0f, text.Length * 0.08f);
        yield return new WaitForSeconds(estimatedDuration);
    }
#endif

    /// <summary>
    /// Detiene cualquier discurso en curso y limpia la cola
    /// </summary>
    public void StopSpeaking()
    {
        StopAllCoroutines();
        textQueue.Clear();
        isSpeaking = false;
    }

    /// <summary>
    /// Indica si hay alguna síntesis de voz actualmente en progreso
    /// </summary>
    public bool IsSpeaking()
    {
        return isSpeaking;
    }
}