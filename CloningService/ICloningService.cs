namespace CloningService
{
    public interface ICloningService
    {
        T Clone<T>(T source);
    }
}