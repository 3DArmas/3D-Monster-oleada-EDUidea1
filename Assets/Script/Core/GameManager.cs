using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    Preparing,  // cuenta atras antes de la primera ronda
    Round,      // ronda en curso
    Shop,       // tienda abierta esperando LISTO
    GameOver    // el jugador ha muerto
}

/// <summary>
/// Orquesta la partida completa: preparacion, rondas, tienda, muerte y reinicio.
/// Es el unico sitio donde vive el estado global del juego.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private RoundManager roundManager;
    [SerializeField] private MoneySystem moneySystem;
    [SerializeField] private PlayerHealth playerHealth;
    [Tooltip("Objeto raiz de la tienda. Se activa al superar cada ronda.")]
    [SerializeField] private GameObject shopRoot;

    [Header("Ajustes")]
    [Tooltip("Segundos de calma antes de la primera ronda.")]
    [SerializeField] private float prepareTime = 3f;
    [Tooltip("0 = la tienda no se cierra sola (GDD v1.1: sin temporizador).")]
    [SerializeField] private float shopAutoCloseTime = 0f;
    [Tooltip("Congela el juego al morir.")]
    [SerializeField] private bool freezeOnDeath = true;

    public GameState State { get; private set; } = GameState.Preparing;
    public int Kills { get; private set; }
    public int MoneyEarned { get; private set; }
    public float ElapsedTime => Time.time - startTime;

    public event Action<GameState> OnStateChanged;
    public event Action<int> OnRoundStarted;                       // numero de ronda
    public event Action<int, int> OnRemainingChanged;              // (vivos+pendientes, total de la ronda)
    public event Action<int, int, int, float> OnGameOver;          // (ronda, bajas, dinero ganado, tiempo)

    private float startTime;
    private Coroutine shopTimer;

    private void OnEnable()
    {
        if (roundManager != null)
        {
            roundManager.OnRoundStarted += HandleRoundStarted;
            roundManager.OnRoundCleared += HandleRoundCleared;
            roundManager.OnAliveChanged += HandleAliveChanged;
        }
        if (playerHealth != null) playerHealth.OnDied += HandlePlayerDied;
        ZombieController.OnAnyZombieDied += HandleZombieDied;
    }

    private void OnDisable()
    {
        if (roundManager != null)
        {
            roundManager.OnRoundStarted -= HandleRoundStarted;
            roundManager.OnRoundCleared -= HandleRoundCleared;
            roundManager.OnAliveChanged -= HandleAliveChanged;
        }
        if (playerHealth != null) playerHealth.OnDied -= HandlePlayerDied;
        ZombieController.OnAnyZombieDied -= HandleZombieDied;
    }

    private void Start()
    {
        startTime = Time.time;
        Kills = 0;
        MoneyEarned = 0;
        SetShopVisible(false);
        SetState(GameState.Preparing);
        StartCoroutine(PrepareThenStart());
    }

    private IEnumerator PrepareThenStart()
    {
        if (prepareTime > 0f) yield return new WaitForSeconds(prepareTime);
        if (State == GameState.GameOver) yield break;

        SetState(GameState.Round);
        roundManager.BeginNextRound();
    }

    /// <summary>Lo llama el boton LISTO de la tienda.</summary>
    public void ContinueToNextRound()
    {
        if (State != GameState.Shop) return;

        if (shopTimer != null) { StopCoroutine(shopTimer); shopTimer = null; }
        SetShopVisible(false);
        SetState(GameState.Round);
        roundManager.BeginNextRound();
    }

    /// <summary>Reinicia la partida desde cero.</summary>
    public void RestartGame()
    {
        Time.timeScale = 1f;
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
    }

    public void SetShopVisible(bool visible)
    {
        if (shopRoot != null) shopRoot.SetActive(visible);
    }

    private void SetState(GameState state)
    {
        if (State == state) return;
        State = state;
        OnStateChanged?.Invoke(State);
    }

    private void HandleRoundStarted(int round)
    {
        OnRoundStarted?.Invoke(round);
        PushRemaining();
    }

    private void HandleAliveChanged(int alive)
    {
        PushRemaining();
    }

    private void PushRemaining()
    {
        if (roundManager == null) return;
        int remaining = roundManager.AliveCount + roundManager.PendingToSpawn;
        OnRemainingChanged?.Invoke(remaining, roundManager.TotalThisRound);
    }

    private void HandleRoundCleared(int round)
    {
        SetState(GameState.Shop);
        SetShopVisible(true);

        if (shopAutoCloseTime > 0f)
        {
            if (shopTimer != null) StopCoroutine(shopTimer);
            shopTimer = StartCoroutine(AutoCloseShop());
        }
    }

    private IEnumerator AutoCloseShop()
    {
        yield return new WaitForSeconds(shopAutoCloseTime);
        shopTimer = null;
        ContinueToNextRound();
    }

    private void HandleZombieDied(ZombieController zombie)
    {
        if (State == GameState.GameOver) return;

        Kills++;
        if (zombie != null && moneySystem != null)
        {
            MoneyEarned += zombie.Reward;
            moneySystem.Add(zombie.Reward);
        }
    }

    private void HandlePlayerDied()
    {
        if (State == GameState.GameOver) return;

        if (roundManager != null) roundManager.StopRound();
        SetShopVisible(false);
        SetState(GameState.GameOver);

        int round = roundManager != null ? Mathf.Max(1, roundManager.CurrentRound) : 1;
        OnGameOver?.Invoke(round, Kills, MoneyEarned, ElapsedTime);

        if (freezeOnDeath) Time.timeScale = 0f;
    }
}
