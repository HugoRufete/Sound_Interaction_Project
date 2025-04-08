using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Gestor de reconocimiento de voz usando Google Speech-to-Text API
/// </summary>
public class GoogleSpeechManager : MonoBehaviour
{
    [SerializeField] private string apiKey = "TU_API_KEY"; // Tu clave de API de Google
    [SerializeField] private string languageCode = "es-ES"; // Código de idioma español
    [SerializeField] private float recordingTime = 5f; // Tiempo de grabación en segundos
    [SerializeField] private int sampleRate = 16000; // Tasa de muestreo (16kHz recomendado para Google Speech)
    [SerializeField] private bool singleWordMode = true; // Si solo esperamos una palabra (un número)
    [SerializeField] private bool autoStart = false; // Iniciar automáticamente en Start

    // Evento para comunicar los resultados del reconocimiento
    public delegate void SpeechRecognizedDelegate(string text);
    public event SpeechRecognizedDelegate OnSpeechRecognized;

    // Evento para notificar cuando comienza y termina la grabación
    public delegate void RecordingStateChangedDelegate(bool isRecording);
    public event RecordingStateChangedDelegate OnRecordingStateChanged;

    private bool isRecording = false;
    private AudioClip recordingClip;

    private static GoogleSpeechManager _instance;
    public static GoogleSpeechManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("GoogleSpeechManager");
                _instance = go.AddComponent<GoogleSpeechManager>();
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

        // Verificar que la API Key está configurada
        if (string.IsNullOrEmpty(apiKey) || apiKey == "TU_API_KEY")
        {
            Debug.LogError("Google Speech API Key no configurada. Por favor, asigna tu clave de API.");
        }
    }

    private void Start()
    {
        if (autoStart)
        {
            StartListening();
        }
    }

    /// <summary>
    /// Inicia la grabación de audio para el reconocimiento de voz
    /// </summary>
    public void StartListening()
    {
        if (isRecording) return;

        if (string.IsNullOrEmpty(apiKey) || apiKey == "TU_API_KEY")
        {
            Debug.LogError("Google Speech API Key no configurada.");
            return;
        }

        StartCoroutine(RecordAudio());
    }

    /// <summary>
    /// Detiene la grabación manualmente antes de que termine el tiempo
    /// </summary>
    public void StopListening()
    {
        if (!isRecording) return;

        isRecording = false;
        OnRecordingStateChanged?.Invoke(false);

        Microphone.End(null);

        // Procesar el audio grabado hasta el momento
        ProcessRecordedAudio();
    }

    private IEnumerator RecordAudio()
    {
        isRecording = true;
        OnRecordingStateChanged?.Invoke(true);

        Debug.Log("Iniciando grabación...");

        // Verificar permisos de micrófono
        if (Application.HasUserAuthorization(UserAuthorization.Microphone) == false)
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);

            if (Application.HasUserAuthorization(UserAuthorization.Microphone) == false)
            {
                Debug.LogError("No se concedió permiso para usar el micrófono");
                isRecording = false;
                OnRecordingStateChanged?.Invoke(false);
                yield break;
            }
        }

        // Verificar que haya micrófonos disponibles
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("No se detectaron micrófonos en el sistema");
            isRecording = false;
            OnRecordingStateChanged?.Invoke(false);
            yield break;
        }

        // Iniciar grabación
        recordingClip = Microphone.Start(null, false, Mathf.CeilToInt(recordingTime), sampleRate);

        if (recordingClip == null)
        {
            Debug.LogError("No se pudo iniciar la grabación");
            isRecording = false;
            OnRecordingStateChanged?.Invoke(false);
            yield break;
        }

        Debug.Log("Grabando audio...");

        // Esperar el tiempo de grabación
        float startTime = Time.time;
        while (isRecording && Time.time < startTime + recordingTime)
        {
            yield return null;
        }

        // Detener grabación si aún está activa
        if (isRecording)
        {
            isRecording = false;
            OnRecordingStateChanged?.Invoke(false);

            Microphone.End(null);
            Debug.Log("Grabación finalizada");

            // Procesar el audio grabado
            ProcessRecordedAudio();
        }
    }

    private void ProcessRecordedAudio()
    {
        if (recordingClip == null) return;

        // Convertir el AudioClip a datos de audio para enviar a Google
        float[] samples = new float[recordingClip.samples];
        recordingClip.GetData(samples, 0);

        // Convertir a PCM de 16 bits
        byte[] audioData = ConvertAudioClipToBytes(samples);

        // Enviar a Google Speech-to-Text API
        StartCoroutine(SendAudioToGoogle(audioData));
    }

    private IEnumerator SendAudioToGoogle(byte[] audioData)
    {
        Debug.Log("Enviando audio a Google Speech-to-Text...");

        // Codificar audio en Base64
        string base64Audio = Convert.ToBase64String(audioData);

        // Construir el JSON para la solicitud a Google
        StringBuilder jsonBuilder = new StringBuilder();
        jsonBuilder.Append("{");
        jsonBuilder.Append("\"config\": {");
        jsonBuilder.Append($"\"languageCode\": \"{languageCode}\",");

        if (singleWordMode)
        {
            // Optimizar para el reconocimiento de palabras individuales (números)
            jsonBuilder.Append("\"model\": \"command_and_search\",");
            jsonBuilder.Append("\"speechContexts\": [{");
            jsonBuilder.Append("\"phrases\": [\"0\", \"1\", \"2\", \"3\", \"4\", \"5\", \"6\", \"7\", \"8\", \"9\", \"10\", \"20\", \"30\", \"40\", \"50\", \"60\", \"70\", \"80\", \"90\", \"100\", \"cero\", \"uno\", \"dos\", \"tres\", \"cuatro\", \"cinco\", \"seis\", \"siete\", \"ocho\", \"nueve\", \"diez\", \"veinte\", \"treinta\", \"cuarenta\", \"cincuenta\", \"sesenta\", \"setenta\", \"ochenta\", \"noventa\", \"cien\"],");
            jsonBuilder.Append("\"boost\": 20");
            jsonBuilder.Append("}]");
        }

        jsonBuilder.Append("},");
        jsonBuilder.Append("\"audio\": {");
        jsonBuilder.Append($"\"content\": \"{base64Audio}\"");
        jsonBuilder.Append("}");
        jsonBuilder.Append("}");

        string jsonRequest = jsonBuilder.ToString();

        // URL de la API de Google Speech-to-Text
        string url = "https://speech.googleapis.com/v1/speech:recognize?key=" + apiKey;

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] jsonToSend = Encoding.UTF8.GetBytes(jsonRequest);
            www.uploadHandler = new UploadHandlerRaw(jsonToSend);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError ||
                www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error al enviar audio a Google: " + www.error);
                Debug.LogError("Respuesta: " + www.downloadHandler.text);
            }
            else
            {
                // Procesar respuesta
                string jsonResponse = www.downloadHandler.text;
                Debug.Log("Respuesta de Google: " + jsonResponse);

                // Extraer el texto reconocido
                string recognizedText = ExtractTextFromGoogleResponse(jsonResponse);

                if (!string.IsNullOrEmpty(recognizedText))
                {
                    Debug.Log("Texto reconocido: " + recognizedText);
                    OnSpeechRecognized?.Invoke(recognizedText);
                }
                else
                {
                    Debug.Log("No se pudo reconocer ningún texto");
                    OnSpeechRecognized?.Invoke(""); // Enviar string vacío
                }
            }
        }
    }

    /// <summary>
    /// Extrae el texto reconocido de la respuesta JSON de Google
    /// </summary>
    private string ExtractTextFromGoogleResponse(string jsonResponse)
    {
        try
        {
            // Esto es una implementación simplificada. En un proyecto real
            // deberías usar JsonUtility o Newtonsoft.Json para un parsing más robusto

            if (jsonResponse.Contains("\"transcript\":"))
            {
                int startIndex = jsonResponse.IndexOf("\"transcript\":") + "\"transcript\":".Length;
                // Buscar el inicio del valor, después de las comillas
                startIndex = jsonResponse.IndexOf("\"", startIndex) + 1;

                // Buscar el final del valor, en la siguiente comilla
                int endIndex = jsonResponse.IndexOf("\"", startIndex);

                if (startIndex > 0 && endIndex > startIndex)
                {
                    return jsonResponse.Substring(startIndex, endIndex - startIndex);
                }
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            Debug.LogError("Error al extraer texto de la respuesta JSON: " + ex.Message);
            return string.Empty;
        }
    }

    /// <summary>
    /// Convierte los datos de un AudioClip a bytes en formato PCM de 16 bits
    /// </summary>
    private byte[] ConvertAudioClipToBytes(float[] samples)
    {
        // Convertir a PCM de 16 bits (LINEAR16)
        Int16[] intData = new Int16[samples.Length];

        for (int i = 0; i < samples.Length; i++)
        {
            // Convertir de -1.0f a 1.0f a valores de 16 bits
            intData[i] = (short)(samples[i] * 32767f);
        }

        // Convertir Int16 a bytes
        byte[] bytesData = new byte[intData.Length * 2];
        Buffer.BlockCopy(intData, 0, bytesData, 0, bytesData.Length);

        return bytesData;
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

        // Manejar números compuestos como "treinta y cinco"
        if (word.Contains("y"))
        {
            string[] parts = word.Split(new string[] { "y" }, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                string decenas = parts[0].Trim();
                string unidades = parts[1].Trim();

                if (numberWords.TryGetValue(decenas, out int decenasValue) &&
                    numberWords.TryGetValue(unidades, out int unidadesValue))
                {
                    if (decenasValue >= 20 && decenasValue % 10 == 0 && unidadesValue < 10)
                    {
                        number = decenasValue + unidadesValue;
                        return true;
                    }
                }
            }
        }

        number = 0;
        return false;
    }
}