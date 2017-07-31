namespace FastNet.Memory
{
    public sealed class CheckableObjectPool<T> : AbstractObjectPoolWrap<T>
        where T : IPoolable
    {
        public CheckableObjectPool(IObjectPool<T> origin) : base(origin)
        {
        }

        public override T Acquire()
        {
            var obj = base.Acquire();
            obj.CheckOut();
            return obj;
        }

        public override void Release(T obj)
        {
            base.Release(obj);
            obj.CheckIn();
        }
    }
}