public interface IDamageable
{
    void TakeDamage(float damageAmount);

    /// <param name="attackerPlayerIndex">Player that caused the damage (-1 when unknown).</param>
    void TakeDamage(float damageAmount, int attackerPlayerIndex);
}
