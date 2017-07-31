namespace FastNet.Memory
{
    public interface IPoolable
    {
        void CheckIn();

        void CheckOut();
    }
}