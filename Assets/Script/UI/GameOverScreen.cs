using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pantalla de fin de partida: muestra el resumen (ronda, bajas, dinero y tiempo)
/// y permite reiniciar. Se suscribe al evento OnGameOver de GameManager.
/// El objeto con este script debe estar ACTIVO; el panel hijo es el que se oculta.
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Text statsText;
    [SerializeField] private Text titleText;
    [SerializeField] private Button restartButton;
    [SerializeField] private GameManager gameManager;

    [Header("Textos")]
    [SerializeField] private string title = "HAS MUERTO";

    private void OnEnable()
    {
        if (gameManager != null) gameManager.OnGameOver += Show;
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
    }

    private void OnDisable()
    {
        if (gameManager != null) gameManager.OnGameOver -= Show;
        if (restartButton != null) restartButton.onClick.RemoveListener(Restart);
    }

    private void Start()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void Show(int round, int kills, int moneyEarned, float elapsedSeconds)
    {
        if (panel != null) panel.SetActive(true);
        if (titleText != null) titleText.text = title;

        if (statsText != null)
        {
            statsText.text =
                "RONDA ALCANZADA      " + round + "\n" +
                "ZOMBIES ELIMINADOS   " + kills + "\n" +
                "DINERO GANADO        $" + moneyEarned + "\n" +
                "TIEMPO               " + FormatTime(elapsedSeconds);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>Lo llama el boton VOLVER A JUGAR.</summary>
    public void Restart()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (gameManager != null) gameManager.RestartGame();
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }
}
