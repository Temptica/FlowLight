using System;

namespace FlowLight.Core.Economy;

public readonly record struct Transaction(float Amount, string Reason);

public sealed class Treasury
{
    public Treasury(float startingBalance)
    {
        Balance = startingBalance;
    }

    public float Balance { get; private set; }

    public event Action<Transaction>? Changed;

    public bool CanAfford(float amount) => Balance >= amount;

    public void Earn(float amount, string reason) => Apply(new Transaction(amount, reason));

    public void Charge(float amount, string reason) => Apply(new Transaction(-amount, reason));

    public bool TrySpend(float amount, string reason)
    {
        if (!CanAfford(amount))
            return false;

        Charge(amount, reason);
        return true;
    }

    private void Apply(Transaction transaction)
    {
        Balance += transaction.Amount;
        Changed?.Invoke(transaction);
    }
}
