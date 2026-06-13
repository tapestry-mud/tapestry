namespace Tapestry.Server;

public class ConnectionLimiter
{
    private int _count;
    private readonly int _max;

    public ConnectionLimiter(int max)
    {
        _max = max;
    }

    public int Current => Volatile.Read(ref _count);

    public bool TryAcquire()
    {
        int original;
        int updated;
        do
        {
            original = Volatile.Read(ref _count);
            if (original >= _max)
            {
                return false;
            }
            updated = original + 1;
        }
        while (Interlocked.CompareExchange(ref _count, updated, original) != original);
        return true;
    }

    public void Release()
    {
        Interlocked.Decrement(ref _count);
    }
}
