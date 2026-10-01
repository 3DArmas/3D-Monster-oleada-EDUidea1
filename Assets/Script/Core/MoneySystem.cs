using System;
using UnityEngine;

/// <summary>
/// Dinero de la partida. Se gana eliminando zombies y se gasta en la tienda.
/// </summary>
public class MoneySystem : MonoBehaviour
{
    [SerializeField] private int startingMoney = 0;

    public int Money { get; private set; }

    /// <summary>Se dispara cada vez que cambia el saldo (lo usa el HUD).</summary>
    public event Action<int> OnMoneyChanged;

    /// <summary>Se dispara cuando el jugador intenta comprar sin saldo suficiente.</summary>
    public event Action<int> OnNotEnoughMoney;

    private void Awake()
    {
        Money = Mathf.Max(0, startingMoney);
    }

    private void Start()
    {
        // Avisa del valor inicial cuando todo esta ya inicializado.
        OnMoneyChanged?.Invoke(Money);
    }

    public void Add(int amount)
    {
        if (amount <= 0) return;
        Money += amount;
        OnMoneyChanged?.Invoke(Money);
    }

    public bool CanAfford(int cost) => cost <= 0 || Money >= cost;

    /// <summary>Intenta gastar. Devuelve false (y avisa) si no hay saldo.</summary>
    public bool TrySpend(int cost)
    {
        if (cost <= 0) return true;

        if (Money < cost)
        {
            OnNotEnoughMoney?.Invoke(cost);
            return false;
        }

        Money -= cost;
        OnMoneyChanged?.Invoke(Money);
        return true;
    }

    public void ResetMoney()
    {
        Money = 0;
        OnMoneyChanged?.Invoke(Money);
    }
}
