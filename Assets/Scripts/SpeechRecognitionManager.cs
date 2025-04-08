using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Windows.Speech; // Para reconocimiento de voz nativo de Windows

public class SpeechRecognitionManager : MonoBehaviour
{
    // Evento para comunicar los resultados del reconocimiento
    public delegate void SpeechRecognizedDelegate(string text);
    public event SpeechRecognizedDelegate OnSpeechRecognized;

    // Reconocimiento de voz nativo de Windows
    private DictationRecognizer dictationRecognizer;
    private bool isListening = false;

    private static SpeechRecognitionManager _instance;
    public static SpeechRecognitionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("SpeechRecognitionManager");
                _instance = go.AddComponent<SpeechRecognitionManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // Propiedad para obtener el estado actual del reconocimiento de voz
    public SpeechSystemStatus Status
    {
        get
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return dictationRecognizer != null ? dictationRecognizer.Status : SpeechSystemStatus.Stopped;
#else
            return SpeechSystemStatus.Stopped;
#endif
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

        // Inicializar reconocimiento de voz
        InitializeSpeechRecognition();
    }

    private void InitializeSpeechRecognition()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        // Inicializar DictationRecognizer para Windows
        dictationRecognizer = new DictationRecognizer();

        dictationRecognizer.DictationResult += (text, confidence) =>
        {
            Debug.Log($"Dictado reconocido: {text} (confianza: {confidence})");

            // Debug adicional para verificar si se está disparando el evento
            Debug.Log($"[DEBUG SpeechRecognition] Enviando evento OnSpeechRecognized con texto: '{text}'");

            // Asegurarnos de que hay suscriptores antes de invocar
            if (OnSpeechRecognized != null)
            {
                OnSpeechRecognized.Invoke(text);
            }
            else
            {
                Debug.LogError("[ERROR] No hay suscriptores al evento OnSpeechRecognized");
            }
        };

        dictationRecognizer.DictationComplete += (completionCause) =>
        {
            // Si se detiene por alguna razón, podemos reiniciarlo
            Debug.Log($"Dictado completado: {completionCause}");

            if (isListening && completionCause != DictationCompletionCause.Complete)
            {
                Debug.Log("Reiniciando reconocimiento automáticamente después de completarse.");
                StartCoroutine(ReiniciarReconocimiento(0.5f));
            }
        };

        dictationRecognizer.DictationError += (error, hresult) =>
        {
            Debug.LogError($"Error de dictado: {error}");

            // Intentar reiniciar en caso de error
            if (isListening)
            {
                Debug.Log("Intentando reiniciar reconocimiento después de error.");
                StartCoroutine(ReiniciarReconocimiento(1.0f));
            }
        };

        Debug.Log("Sistema de dictado inicializado");
#else
        Debug.LogWarning("El reconocimiento de voz nativo de Windows solo está disponible en plataformas Windows");
#endif
    }

    /// <summary>
    /// Corrutina para reiniciar el reconocimiento después de un delay
    /// </summary>
    private IEnumerator ReiniciarReconocimiento(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (isListening)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (dictationRecognizer != null && dictationRecognizer.Status != SpeechSystemStatus.Running)
            {
                Debug.Log("Reiniciando reconocimiento de voz tras delay...");
                dictationRecognizer.Start();
            }
#endif
        }
    }

    /// <summary>
    /// Inicia el reconocimiento de voz
    /// </summary>
    public void StartListening()
    {
        if (isListening) return;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (dictationRecognizer != null && dictationRecognizer.Status != SpeechSystemStatus.Running)
        {
            dictationRecognizer.Start();
            isListening = true;
            Debug.Log("Reconocimiento de voz iniciado");
        }
#elif UNITY_WEBGL
        // Aquí implementaríamos el reconocimiento para WebGL
        Debug.LogWarning("Reconocimiento de voz en WebGL no implementado");
#elif UNITY_ANDROID
        // Aquí implementaríamos el reconocimiento para Android
        Debug.LogWarning("Reconocimiento de voz en Android no implementado");
#elif UNITY_IOS
        // Aquí implementaríamos el reconocimiento para iOS
        Debug.LogWarning("Reconocimiento de voz en iOS no implementado");
#else
        Debug.LogWarning("Reconocimiento de voz no implementado para esta plataforma");
#endif
    }

    /// <summary>
    /// Detiene el reconocimiento de voz
    /// </summary>
    public void StopListening()
    {
        if (!isListening) return;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (dictationRecognizer != null && dictationRecognizer.Status == SpeechSystemStatus.Running)
        {
            dictationRecognizer.Stop();
            isListening = false;
            Debug.Log("Reconocimiento de voz detenido");
        }
#endif
    }

    /// <summary>
    /// Intenta extraer un número de un texto
    /// </summary>
    public bool TryExtractNumber(string text, out int number)
    {
        number = 0;

        if (string.IsNullOrEmpty(text))
            return false;

        // Dividir el texto en palabras
        string[] words = text.Split(new char[] { ' ', ',', '.', ':', ';' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string word in words)
        {
            // Intentar convertir directamente
            if (int.TryParse(word, out number))
            {
                return true;
            }

            // Si no funciona, intentar convertir palabras numéricas en español a números
            if (TryParseSpanishNumberWord(word, out number))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Intenta convertir una palabra numérica en español a su valor numérico
    /// </summary>
    private bool TryParseSpanishNumberWord(string word, out int number)
    {
        word = word.ToLower().Trim();

        // Diccionario de palabras numéricas en español
        Dictionary<string, int> numberWords = new Dictionary<string, int>
        {
            { "cero", 0 },
            { "uno", 1 },
            { "dos", 2 },
            { "tres", 3 },
            { "cuatro", 4 },
            { "cinco", 5 },
            { "seis", 6 },
            { "siete", 7 },
            { "ocho", 8 },
            { "nueve", 9 },
            { "diez", 10 },
            { "once", 11 },
            { "doce", 12 },
            { "trece", 13 },
            { "catorce", 14 },
            { "quince", 15 },
            { "dieciseis", 16 },
            { "dieciséis", 16 },
            { "diecisiete", 17 },
            { "dieciocho", 18 },
            { "diecinueve", 19 },
            { "veinte", 20 },
            { "treinta", 30 },
            { "cuarenta", 40 },
            { "cincuenta", 50 },
            { "sesenta", 60 },
            { "setenta", 70 },
            { "ochenta", 80 },
            { "noventa", 90 },
            { "cien", 100 }
        };

        if (numberWords.TryGetValue(word, out number))
        {
            return true;
        }

        // Intentar manejar compuestos como "veintiuno", "veintidós", etc.
        if (word.StartsWith("veinti"))
        {
            string secondPart = word.Substring(6);
            if (numberWords.TryGetValue(secondPart, out int secondNumber))
            {
                number = 20 + secondNumber;
                return true;
            }
        }

        number = 0;
        return false;
    }

    private void OnDestroy()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (dictationRecognizer != null)
        {
            dictationRecognizer.DictationResult -= (text, confidence) => { };
            dictationRecognizer.DictationComplete -= (completionCause) => { };
            dictationRecognizer.DictationError -= (error, hresult) => { };

            if (dictationRecognizer.Status == SpeechSystemStatus.Running)
            {
                dictationRecognizer.Stop();
            }

            dictationRecognizer.Dispose();
            dictationRecognizer = null;
        }
#endif
    }
}