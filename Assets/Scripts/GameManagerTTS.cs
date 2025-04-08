using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; // Para reiniciar la escena
using UnityEngine.Windows.Speech;

public class GameManagerTTS : MonoBehaviour
{
    [Header("Configuración del Juego")]
    [SerializeField] private int numeroMinimo = 0;
    [SerializeField] private int numeroMaximo = 100;
    [SerializeField] private int intentosMaximos = 5;
    [SerializeField] private float tiempoEsperaEntreMensajes = 0.5f;

    [Header("Ajustes de Debug")]
    [SerializeField] private bool mostrarNumeroSecreto = true;
    [SerializeField] private bool reiniciarEscenaEnNuevoJuego = true; // Opción para reiniciar escena en lugar de solo la lógica

    // Variables del juego
    private int numeroSecreto;
    private int intentosRestantes;
    private int ultimoNumeroIntentado = -1;
    private bool juegoActivo = false;
    private bool esperandoRespuestaNuevoJuego = false;

    // Referencia al sistema de reconocimiento de voz
    private SpeechRecognitionManager speechRecognition;

    // Para verificar si el TTS está listo
    private bool ttsPrepared = false;

    void Start()
    {
        Debug.Log("[GameManagerTTS] Iniciando...");

        // Asegurarnos de que el TTSManager está inicializado
        if (TTSManager.Instance != null)
        {
            ttsPrepared = true;
            Debug.Log("[GameManagerTTS] TTSManager inicializado correctamente");
        }
        else
        {
            Debug.LogError("[GameManagerTTS] ¡Error! No se pudo inicializar TTSManager");
        }

        // Inicializar el sistema de reconocimiento de voz
        speechRecognition = FindObjectOfType<SpeechRecognitionManager>();
        if (speechRecognition == null)
        {
            // Si no existe, crear uno
            GameObject speechRecObj = new GameObject("SpeechRecognitionManager");
            speechRecognition = speechRecObj.AddComponent<SpeechRecognitionManager>();
            Debug.Log("[GameManagerTTS] Creando nuevo SpeechRecognitionManager");
        }
        else
        {
            Debug.Log("[GameManagerTTS] SpeechRecognitionManager encontrado");
        }

        // Suscribirse al evento de reconocimiento de voz
        speechRecognition.OnSpeechRecognized += HandleSpeechRecognized;
        Debug.Log("[GameManagerTTS] Suscrito al evento OnSpeechRecognized");

        // Asegurarse de que no hay reconocimiento activo al iniciar
        speechRecognition.StopListening();

        // Iniciar el juego
        IniciarJuego();
    }

    private void OnDestroy()
    {
        // Desuscribirse del evento al destruir el objeto
        if (speechRecognition != null)
        {
            speechRecognition.OnSpeechRecognized -= HandleSpeechRecognized;
            Debug.Log("[GameManagerTTS] Desuscrito del evento OnSpeechRecognized");
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
            Debug.Log($"[DEBUG] Número secreto: {numeroSecreto}");
        }

        // Detener cualquier síntesis anterior
        if (ttsPrepared)
        {
            TTSManager.Instance.StopSpeaking();
        }

        // Reproducir instrucciones iniciales
        StartCoroutine(DarInstruccionesIniciales());
    }

    private IEnumerator DarInstruccionesIniciales()
    {
        Debug.Log("[GameManagerTTS] Dando instrucciones iniciales");

        // Reproducir instrucciones mediante TTS
        string instrucciones = $"He elegido un numero entre {numeroMinimo} y {numeroMaximo}. Tienes {intentosMaximos} intentos para adivinarlo. Dime un numero.";

        if (ttsPrepared)
        {
            TTSManager.Instance.Speak(instrucciones);

            // Esperar a que termine de hablar
            while (TTSManager.Instance.IsSpeaking())
            {
                yield return null;
            }
        }
        else
        {
            Debug.LogWarning("[GameManagerTTS] TTS no preparado, simulando espera");
            yield return new WaitForSeconds(1f);
        }

        yield return new WaitForSeconds(tiempoEsperaEntreMensajes);

        // Iniciar reconocimiento de voz
        Debug.Log("[GameManagerTTS] Iniciando reconocimiento de voz después de instrucciones");
        speechRecognition.StartListening();
    }

    private void HandleSpeechRecognized(string text)
    {
        Debug.Log($"[GameManagerTTS] Texto reconocido: {text}");

        // Depuración del estado
        Debug.Log($"[GameManagerTTS] Estado: juegoActivo={juegoActivo}, esperandoRespuestaNuevoJuego={esperandoRespuestaNuevoJuego}");

        if (esperandoRespuestaNuevoJuego)
        {
            Debug.Log($"[GameManagerTTS] Procesando respuesta para nuevo juego: '{text}'");
            ProcesarRespuestaNuevoJuego(text);
            return;
        }

        if (!juegoActivo) return;

        // Procesar texto como un intento del jugador
        ProcesarEntradaUsuario(text);
    }

    private void ProcesarEntradaUsuario(string texto)
    {
        // Detener reconocimiento mientras procesamos la entrada
        speechRecognition.StopListening();
        Debug.Log("[GameManagerTTS] Reconocimiento detenido para procesar entrada");

        // Intentar extraer un número del texto
        int numeroIntentado;
        if (speechRecognition.TryExtractNumber(texto, out numeroIntentado))
        {
            Debug.Log($"[GameManagerTTS] Número extraído: {numeroIntentado}");

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
            Debug.Log("[GameManagerTTS] No se pudo extraer un número del texto");
            // No se reconoció un número
            StartCoroutine(ProcesarTextoNoReconocido());
        }
    }

    private IEnumerator ProcesarNumeroInvalido()
    {
        Debug.Log("[GameManagerTTS] Procesando número inválido");

        if (ttsPrepared)
        {
            TTSManager.Instance.Speak($"Solo son válidos numeros entre {numeroMinimo} y {numeroMaximo}");

            // Esperar a que termine de hablar
            while (TTSManager.Instance.IsSpeaking())
            {
                yield return null;
            }
        }

        yield return new WaitForSeconds(tiempoEsperaEntreMensajes);

        // Volver a escuchar
        Debug.Log("[GameManagerTTS] Reiniciando reconocimiento después de número inválido");
        speechRecognition.StartListening();
    }

    private IEnumerator ProcesarNumeroRepetido()
    {
        Debug.Log("[GameManagerTTS] Procesando número repetido");

        if (ttsPrepared)
        {
            TTSManager.Instance.Speak("Ese numero ya lo has dicho");

            // Esperar a que termine de hablar
            while (TTSManager.Instance.IsSpeaking())
            {
                yield return null;
            }
        }

        yield return new WaitForSeconds(tiempoEsperaEntreMensajes);

        // Volver a escuchar
        Debug.Log("[GameManagerTTS] Reiniciando reconocimiento después de número repetido");
        speechRecognition.StartListening();
    }

    private IEnumerator ProcesarTextoNoReconocido()
    {
        Debug.Log("[GameManagerTTS] Procesando texto no reconocido como número");

        if (ttsPrepared)
        {
            TTSManager.Instance.Speak($"No he entendido. Por favor, dime un numero del {numeroMinimo} al {numeroMaximo}");

            // Esperar a que termine de hablar
            while (TTSManager.Instance.IsSpeaking())
            {
                yield return null;
            }
        }

        yield return new WaitForSeconds(tiempoEsperaEntreMensajes);

        // Volver a escuchar
        Debug.Log("[GameManagerTTS] Reiniciando reconocimiento después de texto no reconocido");
        speechRecognition.StartListening();
    }

    private void ProcesarIntento(int numero)
    {
        intentosRestantes--;

        Debug.Log($"[GameManagerTTS] Procesando intento: {numero}, intentos restantes: {intentosRestantes}");

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
        Debug.Log("[GameManagerTTS] Procesando victoria");

        if (ttsPrepared)
        {
            TTSManager.Instance.Speak($"¡Correcto! Has adivinado el numero {numeroSecreto}.");

            // Esperar a que termine de hablar
            while (TTSManager.Instance.IsSpeaking())
            {
                yield return null;
            }
        }

        yield return new WaitForSeconds(tiempoEsperaEntreMensajes);

        // Preguntar si quiere jugar otra partida
        Debug.Log("[GameManagerTTS] Preguntando si quiere jugar de nuevo después de victoria");
        PreguntarNuevoJuego();
    }

    private IEnumerator ProcesarDerrota()
    {
        Debug.Log("[GameManagerTTS] Procesando derrota");

        if (ttsPrepared)
        {
            TTSManager.Instance.Speak($"Has agotado tus intentos. El numero era {numeroSecreto}.");

            // Esperar a que termine de hablar
            while (TTSManager.Instance.IsSpeaking())
            {
                yield return null;
            }
        }

        yield return new WaitForSeconds(tiempoEsperaEntreMensajes);

        // Preguntar si quiere jugar otra partida
        Debug.Log("[GameManagerTTS] Preguntando si quiere jugar de nuevo después de derrota");
        PreguntarNuevoJuego();
    }

    private IEnumerator ProcesarMayorMenor(bool esMayor)
    {
        string mensaje = esMayor ? "Mayor" : "Menor";
        Debug.Log($"[GameManagerTTS] Procesando {mensaje}, intentos restantes: {intentosRestantes}");

        // Crear la frase en formato natural
        string textoIntentos = intentosRestantes == 1 ? "intento restante" : "intentos restantes";

        if (ttsPrepared)
        {
            TTSManager.Instance.Speak($"{mensaje}. Te quedan {intentosRestantes} {textoIntentos}.");

            // Esperar a que termine de hablar
            while (TTSManager.Instance.IsSpeaking())
            {
                yield return null;
            }
        }

        yield return new WaitForSeconds(tiempoEsperaEntreMensajes);

        // Volver a escuchar
        Debug.Log("[GameManagerTTS] Reiniciando reconocimiento después de Mayor/Menor");
        speechRecognition.StartListening();
    }

    private void PreguntarNuevoJuego()
    {
        Debug.Log("[GameManagerTTS] Entrando a PreguntarNuevoJuego()");

        juegoActivo = false;
        esperandoRespuestaNuevoJuego = true;

        Debug.Log($"[GameManagerTTS] esperandoRespuestaNuevoJuego establecida a: {esperandoRespuestaNuevoJuego}");

        // Preguntar si quiere jugar de nuevo - B.7 Preguntar si quieres volver a jugar
        StartCoroutine(PreguntarNuevoJuegoCoroutine());
    }

    private IEnumerator PreguntarNuevoJuegoCoroutine()
    {
        yield return new WaitForSeconds(tiempoEsperaEntreMensajes);

        Debug.Log("[GameManagerTTS] Preguntando si quiere jugar otra vez");

        if (ttsPrepared)
        {
            TTSManager.Instance.Speak("¿Quieres jugar otra vez? Responde Si o No.");

            // Esperar a que termine de hablar
            while (TTSManager.Instance.IsSpeaking())
            {
                yield return null;
            }
        }

        yield return new WaitForSeconds(tiempoEsperaEntreMensajes);

        // Reiniciar el reconocimiento para capturar la respuesta
        Debug.Log("[GameManagerTTS] Iniciando reconocimiento para respuesta de nuevo juego");
        Debug.Log($"[GameManagerTTS] Estado esperandoRespuestaNuevoJuego antes de iniciar reconocimiento: {esperandoRespuestaNuevoJuego}");

        speechRecognition.StartListening();

        // Comprobar periódicamente si el reconocimiento se cancela
        StartCoroutine(ComprobarReconocimientoCancelado());
    }

    private IEnumerator ComprobarReconocimientoCancelado()
    {
        // Esperar un poco antes de empezar a comprobar
        yield return new WaitForSeconds(1.0f);

        while (esperandoRespuestaNuevoJuego)
        {
            // Verificar si el reconocimiento se ha cancelado
            if (speechRecognition.Status != SpeechSystemStatus.Running)
            {
                Debug.Log("[GameManagerTTS] Reconocimiento cancelado mientras se esperaba respuesta. Reiniciando...");
                speechRecognition.StartListening();
            }

            yield return new WaitForSeconds(1.0f);
        }
    }

    private void ProcesarRespuestaNuevoJuego(string respuesta)
    {
        // Detener reconocimiento mientras procesamos
        speechRecognition.StopListening();

        Debug.Log($"[GameManagerTTS] ProcesarRespuestaNuevoJuego - Respuesta: '{respuesta}'");

        respuesta = respuesta.ToLower().Trim();

        // Verificar la respuesta de manera simplificada
        bool respuestaSi = respuesta.Contains("si") || respuesta.Contains("sí") || respuesta == "s";
        bool respuestaNo = respuesta.Contains("no") || respuesta == "n";

        Debug.Log($"[GameManagerTTS] Respuesta interpretada como: {(respuestaSi ? "SÍ" : (respuestaNo ? "NO" : "DESCONOCIDA"))}");

        if (respuestaSi)
        {
            Debug.Log("[GameManagerTTS] Se ha decidido reiniciar el juego");

            if (ttsPrepared)
            {
                TTSManager.Instance.StopSpeaking();
                TTSManager.Instance.Speak("De acuerdo, iniciando nuevo juego.");
            }

            // Reiniciar después de una breve pausa
            if (reiniciarEscenaEnNuevoJuego)
            {
                // Reiniciar toda la escena
                Invoke("ReiniciarEscena", 2.0f);
            }
            else
            {
                // Solo reiniciar la lógica
                Invoke("ReiniciarJuegoDirecto", 2.0f);
            }
        }
        else if (respuestaNo)
        {
            Debug.Log("[GameManagerTTS] Se ha decidido finalizar el juego");
            StartCoroutine(CerrarAplicacion());
        }
        else
        {
            Debug.Log("[GameManagerTTS] Respuesta no reconocida, volviendo a preguntar");

            // Restablecer la variable por si acaso
            esperandoRespuestaNuevoJuego = true;

            StartCoroutine(PreguntarNuevoJuegoCoroutine());
        }
    }

    private void ReiniciarEscena()
    {
        Debug.Log("[GameManagerTTS] Reiniciando escena completa");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void ReiniciarJuegoDirecto()
    {
        Debug.Log("[GameManagerTTS] Reiniciando lógica del juego");

        // Resetear todas las variables de estado
        esperandoRespuestaNuevoJuego = false;

        // Iniciar un nuevo juego
        IniciarJuego();
    }

    private IEnumerator CerrarAplicacion()
    {
        Debug.Log("[GameManagerTTS] Preparando cierre de aplicación");

        if (ttsPrepared)
        {
            TTSManager.Instance.Speak("Gracias por jugar. Hasta pronto.");

            // Esperar a que termine de hablar
            while (TTSManager.Instance.IsSpeaking())
            {
                yield return null;
            }
        }

        yield return new WaitForSeconds(1f);

        Debug.Log("[GameManagerTTS] Cerrando aplicación");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}