using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Configuraci�n")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip introClip; // Audio grabado para instrucciones iniciales
    [SerializeField] private AudioClip clipMayor; // Audio para "Mayor, te quedan"
    [SerializeField] private AudioClip clipMenor; // Audio para "Menor, te quedan"
    [SerializeField] private AudioClip clipVictoria; // Audio para victoria
    [SerializeField] private AudioClip clipDerrota; // Audio para derrota
    [SerializeField] private AudioClip clipNumeroInvalido; // Audio para n�mero inv�lido
    [SerializeField] private AudioClip clipNumeroRepetido; // Audio para n�mero repetido
    [SerializeField] private AudioClip clipIntentos; // Audio para "intentos restantes"
    [SerializeField] private AudioClip clipNuevoJuego; // Audio para preguntar si quiere jugar de nuevo

    [Header("Clips para N�meros")]
    [SerializeField] private AudioClip[] clipsNumeros; // Array de clips para n�meros (0-5)

    [Header("Ajustes del Juego")]
    [SerializeField] private int numeroMinimo = 0;
    [SerializeField] private int numeroMaximo = 100;
    [SerializeField] private int intentosMaximos = 5;

    private int numeroSecreto;
    private int intentosRestantes;
    private int ultimoNumeroIntentado = -1;
    private bool juegoActivo = false;
    private bool esperandoRespuestaNuevoJuego = false;

    // Referencia al sistema de reconocimiento de voz
    private SpeechRecognitionManager speechRecognition;

    void Start()
    {
        // Verificar que tengamos un AudioSource
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            Debug.LogWarning("AudioSource no asignado. Se ha creado uno autom�ticamente.");
        }

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
        // Generar n�mero aleatorio entre 0 y 100
        numeroSecreto = Random.Range(numeroMinimo, numeroMaximo + 1);
        intentosRestantes = intentosMaximos;
        ultimoNumeroIntentado = -1;
        juegoActivo = true;
        esperandoRespuestaNuevoJuego = false;

        Debug.Log($"[DEBUG] N�mero secreto: {numeroSecreto}");

        // Reproducir instrucciones iniciales
        StartCoroutine(DarInstruccionesIniciales());
    }

    private IEnumerator DarInstruccionesIniciales()
    {
        // Si tenemos un clip de audio para las instrucciones, reproducirlo
        if (introClip != null)
        {
            audioSource.clip = introClip;
            audioSource.Play();

            // Esperar a que termine el audio
            yield return new WaitForSeconds(introClip.length);
        }
        else
        {
            // Si no hay audio pregrabado, mostrar mensaje en consola
            Debug.Log("No hay audio de instrucciones asignado. Se deber�a escuchar: 'He elegido un n�mero entre 0 y 100. Tienes 5 intentos para adivinarlo. Dime un n�mero.'");

            // Peque�a espera
            yield return new WaitForSeconds(2f);
        }

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
        // Intentar extraer un n�mero del texto
        int numeroIntentado;
        if (speechRecognition.TryExtractNumber(texto, out numeroIntentado))
        {
            // Verificar que el n�mero est� en el rango v�lido
            if (numeroIntentado < numeroMinimo || numeroIntentado > numeroMaximo)
            {
                ReproducirMensaje(clipNumeroInvalido, $"Solo son v�lidos n�meros entre {numeroMinimo} y {numeroMaximo}");

                // Volver a escuchar tras un breve retraso
                StartCoroutine(ReiniciarEscucha(2f));
                return;
            }

            // Verificar si el n�mero ya fue intentado - B.5 Detectar si se repite el mismo n�mero
            if (numeroIntentado == ultimoNumeroIntentado)
            {
                ReproducirMensaje(clipNumeroRepetido, "Ese n�mero ya lo has dicho");

                // Volver a escuchar tras un breve retraso
                StartCoroutine(ReiniciarEscucha(2f));
                return;
            }

            // Guardar el �ltimo n�mero intentado
            ultimoNumeroIntentado = numeroIntentado;

            // Procesar el intento
            ProcesarIntento(numeroIntentado);
        }
        else
        {
            // No se reconoci� un n�mero
            ReproducirMensaje(clipNumeroInvalido, "No he entendido. Por favor, dime un n�mero del 0 al 100.");

            // Volver a escuchar tras un breve retraso
            StartCoroutine(ReiniciarEscucha(2f));
        }
    }

    private IEnumerator ReiniciarEscucha(float delay)
    {
        // Pausar brevemente antes de volver a escuchar
        yield return new WaitForSeconds(delay);

        // Reiniciar reconocimiento de voz
        speechRecognition.StartListening();
    }

    private void ProcesarIntento(int numero)
    {
        intentosRestantes--;

        Debug.Log($"Procesando intento: {numero}, intentos restantes: {intentosRestantes}");

        if (numero == numeroSecreto)
        {
            // �Victoria!
            ReproducirMensaje(clipVictoria, $"�Correcto! Has adivinado el n�mero {numeroSecreto}.");
            PreguntarNuevoJuego();
        }
        else if (intentosRestantes <= 0)
        {
            // Se acabaron los intentos - B.6 Al terminarse los intentos, desvelar el n�mero
            ReproducirMensaje(clipDerrota, $"Has agotado tus intentos. El n�mero era {numeroSecreto}.");
            PreguntarNuevoJuego();
        }
        else if (numero < numeroSecreto)
        {
            // El n�mero es mayor - B.4 El ordenador responder� con "Mayor" o "Menor" y "Te quedan XXX intentos"
            string mensaje = $"Mayor. Te quedan {intentosRestantes} intentos.";
            StartCoroutine(ReproducirSecuenciaIntentos(true));

            // Volver a escuchar tras reproducir el mensaje
            StartCoroutine(ReiniciarEscucha(4f)); // Aumentamos el tiempo para la secuencia completa
        }
        else
        {
            // El n�mero es menor - B.4 El ordenador responder� con "Mayor" o "Menor" y "Te quedan XXX intentos"
            string mensaje = $"Menor. Te quedan {intentosRestantes} intentos.";
            StartCoroutine(ReproducirSecuenciaIntentos(false));

            // Volver a escuchar tras reproducir el mensaje
            StartCoroutine(ReiniciarEscucha(4f)); // Aumentamos el tiempo para la secuencia completa
        }
    }

    private void PreguntarNuevoJuego()
    {
        juegoActivo = false;
        esperandoRespuestaNuevoJuego = true;

        // Detener reconocimiento temporalmente
        speechRecognition.StopListening();

        // Preguntar si quiere jugar de nuevo - B.7 Preguntar si quieres volver a jugar
        StartCoroutine(PreguntarNuevoJuegoCoroutine());
    }

    private IEnumerator PreguntarNuevoJuegoCoroutine()
    {
        yield return new WaitForSeconds(1f);
        ReproducirMensaje(clipNuevoJuego, "�Quieres jugar otra vez? Responde S� o No.");

        yield return new WaitForSeconds(2f);

        // Reiniciar el reconocimiento para capturar la respuesta
        speechRecognition.StartListening();
    }

    private void ProcesarRespuestaNuevoJuego(string respuesta)
    {
        // Detener reconocimiento mientras procesamos
        speechRecognition.StopListening();

        respuesta = respuesta.ToLower();

        if (respuesta.Contains("si") || respuesta.Contains("s�") || respuesta == "s" || respuesta.Contains("vale"))
        {
            // Reiniciar el juego
            IniciarJuego();
        }
        else if (respuesta.Contains("no") || respuesta == "n")
        {
            // Cerrar la aplicaci�n
            Debug.Log("Cerrando la aplicaci�n");
            StartCoroutine(CerrarAplicacion());
        }
        else
        {
            // No se entendi� la respuesta
            Debug.Log("Respuesta no reconocida: " + respuesta);

            // Volver a preguntar
            StartCoroutine(PreguntarNuevoJuegoCoroutine());
        }
    }

    private IEnumerator CerrarAplicacion()
    {
        yield return new WaitForSeconds(2f);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ReproducirMensaje(AudioClip clip, string mensajeAlternativo)
    {
        // Siempre mostrar el mensaje en la consola para depuraci�n
        Debug.Log($"Mensaje de voz: {mensajeAlternativo}");

        if (clip != null)
        {
            // Si hay un clip de audio, reproducirlo
            audioSource.clip = clip;
            audioSource.Play();
        }
        else
        {
            Debug.LogWarning($"No hay clip de audio asignado para: '{mensajeAlternativo}'. Deber�a crear estos audios y asignarlos al GameManager.");
        }
    }

    /// <summary>
    /// Reproduce una secuencia de audios: "Mayor/Menor, te quedan" + n�mero + "intentos restantes"
    /// </summary>
    private IEnumerator ReproducirSecuenciaIntentos(bool esMayor)
    {
        AudioClip clipRespuesta = esMayor ? clipMayor : clipMenor;

        // 1. Reproducir "Mayor/Menor, te quedan"
        if (clipRespuesta != null)
        {
            audioSource.clip = clipRespuesta;
            audioSource.Play();

            // Esperar a que termine el audio
            yield return new WaitForSeconds(audioSource.clip.length);
        }
        else
        {
            Debug.LogWarning($"No hay clip de audio asignado para: '{(esMayor ? "Mayor" : "Menor")}, te quedan'");
            yield return new WaitForSeconds(0.5f);
        }

        // 2. Reproducir el n�mero de intentos restantes
        if (clipsNumeros != null && intentosRestantes >= 0 && intentosRestantes < clipsNumeros.Length && clipsNumeros[intentosRestantes] != null)
        {
            audioSource.clip = clipsNumeros[intentosRestantes];
            audioSource.Play();

            // Esperar a que termine el audio
            yield return new WaitForSeconds(audioSource.clip.length);
        }
        else
        {
            Debug.LogWarning($"No hay clip de audio asignado para el n�mero: {intentosRestantes}");
            yield return new WaitForSeconds(0.5f);
        }

        // 3. Reproducir "intentos restantes"
        if (clipIntentos != null)
        {
            audioSource.clip = clipIntentos;
            audioSource.Play();

            // Esperar a que termine el audio
            yield return new WaitForSeconds(audioSource.clip.length);
        }
        else
        {
            Debug.LogWarning("No hay clip de audio asignado para: 'intentos restantes'");
        }
    }
}