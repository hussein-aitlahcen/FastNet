using System;

namespace FastNet.Memory
{
    public abstract class AbstractObjectPoolWrap<T> : IObjectPool<T>
    {
        private readonly IObjectPool<T> origin;

        protected AbstractObjectPoolWrap(IObjectPool<T> origin)
        {
            this.origin = origin;
        }

        public virtual T Acquire()
        {
            return this.origin.Acquire();
        }

        public virtual void Release(T obj)
        {
            this.origin.Release(obj);
        }

        public void Dispose()
        {
            this.origin.Dispose();
        }
    }
}