using System;
using FastNet.Memory;

namespace FastNet.Core
{
    using ByteSegment = ArraySegment<byte>;

    public sealed class BufferPool : ObjectPool<ByteSegment>
    {
        public static Func<ByteSegment> CreateSegmentFactory(int segmentCount, int segmentSize)
        {
            var buffer = new byte[segmentCount * segmentSize];
            var offset = 0;
            var maxOffset = segmentCount - 1;
            return () =>
            {
                if (offset >= maxOffset)
                {
                    throw new OverflowException("no more segment boi");
                }
                return new ByteSegment(buffer, offset, segmentSize);
            };
        }

        public BufferPool(int segmentCount, int segmentSize)
                 : base(CreateSegmentFactory(segmentCount, segmentSize), segmentCount)
        {
        }
    }
}