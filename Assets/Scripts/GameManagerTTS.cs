using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManagerTTS : MonoBehaviour
{
    [Header("Configuración del Juego")]
    [SerializeField] private int numeroMinimo = 0;
    [SerializeField] private int numeroMaximo = 100;
    [SerializeField] private int intentosMaximos = 5;
    [SerializeField] private float tiempoEsperaEntreMensajes = 0.5f;

    [Header("Ajustes de Debug")]
    [SerializeField] private bool mostrarNumeroSecreto = true;

    // Variables del juego
    private int numeroSecreto;
    private int intentosRestantes;
    private int ultimoNumeroIntentado = -1;
    private bool juegoActivo = false;
    private bool esperandoRespuestaNuevoJuego = false;

    // Referencia al sistema de reconocimiento de voz
    private SpeechRecognitionManager speechRecognition;

    void Start()
    {
        // Inicializar el sistema de reconocimiento de voz
        speechRecognition = FindObjectOfType<SpeechRecognitionManager>();
        if (speechRecognition == null)
        {
            // Si no existe, crear uno
            GameObject speechRecObj = new GameObject("SpeechRecognitionManager");
            speechRecognition = speechRecObj.AddComponent<SpeechRecognitionManager>();
        }

        // Suscribirse al evento de reconocimiento de voz
        speechRecognition.OnSpeechRecognized += HandleSpeechRecognized;

        // Inicializar el sistema TTS si no existe
        if (TTSManager.Instance == null)
        {
            Debug.LogWarning("TTSManager no encontrado, creando uno nuevo");
        }

        // Iniciar el juego
        IniciarJuego();
    }

    private void OnDestroy()
    {
        // Desuscribirse del evento al destruir el objeto
        if (speechRecognition != null)
        {
            speechRecognition.OnSpeechRecognized -= HandleSpeechRecognized;
        }
    }

    private void IniciarJuego()
    {
        // Generar número aleatorio entre 0 y 100
        numeroSecreto = Random.Range(numeroMinimo, numeroMaximo + 1);
        intentosRestantes = intentosMaximos;
        ultimoNumeroIntentado = -1;
        juegoActivo = true;
        esperandoRespuestaNuevoJuego = false;

        if (mostrarNumeroSecreto)
        {
            Debug.Log($"[DEBUG] Numero secreto: {numeroSecreto}");
        }

        // Reproducir instrucciones iniciales
        StartCoroutine(DarInstruccionesIniciales());
    }

    private IEnumerator DarInstruccionesIniciales()
    {
        // Detener cualquier síntesis anterior
        TTSManager.Instance.StopSpeaking();

        // Reproducir instrucciones mediante TTS
        string instrucciones = $"He elegido un numero entre {numeroMinimo} y {numeroMaximo}. Tienes {intentosMaximos} intentos para adivinarlo. Dime un numero.";
        TTSManager.Instance.Speak(instrucciones);

        // Esperar a que termine de hablar
        while (TTSManager.Instance.IsSpeaking())
        {
            yield return null;
        }

        yield return new WaitForSeconds(tiempoEsperaEntreMensajes);

        // Iniciar reconocimiento de voz
        speechRecognition.StartListening();
    }

    private void HandleSpeechRecognized(string text)
    {
        Debug.Log($"Texto reconocido: {text}");

        if (!juegoActivo) return;

        if (esperandoRespuestaNuevoJuego)
        {
            ProcesarRespuestaNuevoJuego(text);
            return;
        }

        // Procesar texto como un intento del jugador
        ProcesarEntradaUsuario(text);
    }

    private void ProcesarEntradaUsuario(string texto)
    {
        // Detener reconocimiento mientras procesamos la entrada
        speechRecognition.StopListening();

        // Intentar extraer un número del texto
        int numeroIntentado;
        if (speechRecognition.TryExtractNumber(texto, out numeroIntentado))
        {
            // Verificar que el número esté en el rango válido
            if (numeroIntentado < numeroMinimo || numeroIntentado > numeroMaximo)
            {
                StartCoroutine(ProcesarNumeroInvalido());
                return;
            }

            // Verificar si el número ya fue intentado - B.5 Detectar si se repite el mismo número
            if (numeroIntentado == ultimoNumeroIntentado)
            {
                StartCoroutine(ProcesarNumeroRepetido());
                return;
            }

            // Guardar el último número intentado
            ultimoNumeroIntentado = numeroIntentado;

            // Procesar el intento
            ProcesarIntento(numeroIntentado);
        }
        else
        {
            // No se reconoció un número
            StartCoroutine(ProcesarTextoNoReconocido());
        }
    }

    private IEnumerator ProcesarNumeroInvalido()
    {
        TTSManager.Instance.Speak($"Solo son válidos numeros entre {numeroMinimo} y {numeroMaximo}");

        // Esperar a que termine de hablar
        while (TTSManager.Instance.IsSpeaking())
        {
            yield return null;
        }

        yield return new WaitForSeconds(tiempoEsperaEntreMensajes);

        // Volver a escuchar
        speechRecognition.StartListening();
    }

    private IEnumerator ProcesarNumeroRepetido()
    {
        TTSManager.Instance.Speak("Ese numero ya lo has dicho");

        // Esperar a que termine de hablar
        while (TTSManager.Instance.IsSpeaking())
        {
            yield return null;
        }

        yield return new WaitForSeconds(tiempoEsperaEntreMensajes);

        // Volver a escuchar
        speechRecognition.StartListening();
    }

    private IEnumerator ProcesarTextoNoReconocido()
    {
        TTSManager.Instance.Speak($"No he entendido. Por favor, dime un numero del {numeroMinimo} al {numeroMaximo}");

        // Esperar a que termine de hablar
        while (TTSManager.Instance.IsSpeaking())
        {
            yield return null;
        }

        yield return new WaitForSeconds(tiempoEsperaEntreMensajes);

        // Volver a escuchar
        speechRecognition.StartListening();
    }

    private void ProcesarIntento(int numero)
    {
        intentosRestantes--;

        Debug.Log($"Procesando intento: {numero}, intentos restantes: {intentosRestantes}");

        if (numero == numeroSecreto)
        {
            // ¡Victoria!
            StartCoroutine(ProcesarVictoria());
        }
        else if (intentosRestantes <= 0)
        {
            // Se acabaron los intentos - B.6 Al terminarse los intentos, desvelar el número
            StartCoroutine(ProcesarDerrota());
        }
        else if (numero < numeroSecreto)
        {
            // El número es mayor - B.4 El ordenador responderá con "Mayor" o "Menor" y "Te quedan XXX intentos"
            StartCoroutine(ProcesarMayorMenor(true));
        }
        else
        {
            // El número es menor - B.4 El ordenador responderá con "Mayor" o "Menor" y "Te quedan XXX intentos"
            StartCoroutine(ProcesarMayorMenor(false));
        }
    }

    private IEnumerator ProcesarVictoria()
    {
        TTSManager.Instance.Speak($"¡Correcto! Has adivinado el numero {numeroSecreto}.");

        // Esperar a que termine de hablar
        while (TTSManager.Instance.IsSpeaking())
        {
            yield return null;
        }

        yield return new WaitForSeconds(tiempoEsperaEntreMensajes);

        // Preguntar si quiere jugar otra partida
        PreguntarNuevoJuego();
    }

    private IEnumerator ProcesarDerrota()
    {
        TTSManager.Instance.Speak($"Has agotado tus intentos. El numero era {numeroSecreto}.");

        // Esperar a que termine de hablar
        while (TTSManager.Instance.IsSpeaking())
        {
            yield return null;
        }

        yield return new WaitForSeconds(tiempoEsperaEntreMensajes);

        // Preguntar si quiere jugar otra partida
        PreguntarNuevoJuego();
    }

    private IEnumerator ProcesarMayorMenor(bool esMayor)
    {
        string mensaje = esMayor ? "Mayor" : "Menor";

        // Crear la frase en formato natural
        string textoIntentos = intentosRestantes == 1 ? "intento restante" : "intentos restantes";
        TTSManager.Instance.Speak($"{mensaje}. Te quedan {intentosRestantes} {textoIntentos}.");

        // Esperar a que termine de hablar
        while (TTSManager.Instance.IsSpeaking())
        {
            yield return null;
        }

        yield return new WaitForSeconds(tiempoEsperaEntreMensajes);

        // Volver a escuchar
        speechRecognition.StartListening();
    }

    private void PreguntarNuevoJuego()
    {
        juegoActivo = false;
        esperandoRespuestaNuevoJuego = true;

        // Preguntar si quiere jugar de nuevo - B.7 Preguntar si quieres volver a jugar
        StartCoroutine(PreguntarNuevoJuegoCoroutine());
    }

    private IEnumerator PreguntarNuevoJuegoCoroutine()
    {
        yield return new WaitForSeconds(tiempoEsperaEntreMensajes);

        TTSManager.Instance.Speak("¿Quieres jugar otra vez? Responde Si o No.");

        // Esperar a que termine de hablar
        while (TTSManager.Instance.IsSpeaking())
        {
            yield return null;
        }

        yield return new WaitForSeconds(tiempoEsperaEntreMensajes);

        // Reiniciar el reconocimiento para capturar la respuesta
        speechRecognition.StartListening();
    }

    private void ProcesarRespuestaNuevoJuego(string respuesta)
    {
        // Detener reconocimiento mientras procesamos
        speechRecognition.StopListening();

        respuesta = respuesta.ToLower();

        if (respuesta.Contains("si") || respuesta.Contains("sí") || respuesta == "s" || respuesta.Contains("vale"))
        {
            // Reiniciar el juego
            IniciarJuego();
        }
        else if (respuesta.Contains("no") || respuesta == "n")
        {
            // Cerrar la aplicación
            Debug.Log("Cerrando la aplicación");
            StartCoroutine(CerrarAplicacion());
        }
        else
        {
            // No se entendió la respuesta
            Debug.Log("Respuesta no reconocida: " + respuesta);

            // Volver a preguntar
            StartCoroutine(PreguntarNuevoJuegoCoroutine());
        }
    }

    private IEnumerator CerrarAplicacion()
    {
        TTSManager.Instance.Speak("Gracias por jugar. Hasta pronto.");

        // Esperar a que termine de hablar
        while (TTSManager.Instance.IsSpeaking())
        {
            yield return null;
        }

        yield return new WaitForSeconds(1f);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}