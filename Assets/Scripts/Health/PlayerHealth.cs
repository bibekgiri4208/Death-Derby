public class PlayerHealth : Health
{
    protected override void Die()
    {
        ResetHealth();
    }
}