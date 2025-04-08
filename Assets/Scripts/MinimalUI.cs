using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MinimalUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private bool hideAfterStart = true;
    [SerializeField] private float hideDelay = 5f;

    private void Start()
    {
        // Asegúrate de que el texto esté configurado
        if (messageText != null)
        {
            messageText.text = "Conecta tus altavoces y micrófono";

            // Opcionalmente ocultar la UI después de un tiempo
            if (hideAfterStart)
            {
                StartCoroutine(HideUIAfterDelay());
            }
        }
        else
        {
            Debug.LogWarning("No se ha asignado el componente TextMeshProUGUI en MinimalUI");
        }
    }

    private IEnumerator HideUIAfterDelay()
    {
        yield return new WaitForSeconds(hideDelay);

        // Ocultar toda la interfaz
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            // Fade out
            float startTime = Time.time;
            float duration = 1.0f; // 1 segundo para el fade

            while (Time.time < startTime + duration)
            {
                canvasGroup.alpha = Mathf.Lerp(1, 0, (Time.time - startTime) / duration);
                yield return null;
            }

            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        else
        {
            // Si no hay CanvasGroup, simplemente desactivar el objeto
            gameObject.SetActive(false);
        }
    }
}