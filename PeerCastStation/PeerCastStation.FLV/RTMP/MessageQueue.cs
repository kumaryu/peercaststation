using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.FLV.RTMP
{
	class MessageQueue<T>
	{
		private SemaphoreSlim filledLock = new SemaphoreSlim(0, Int32.MaxValue);
		private Queue<T> queue = new Queue<T>();

		public void Enqueue(T item)
		{
			lock (queue) {
				queue.Enqueue(item);
				filledLock.Release();
			}
		}

		public async Task<T> DequeueAsync(CancellationToken cancel_token)
		{
			await filledLock.WaitAsync(cancel_token);
			lock (queue) {
				return queue.Dequeue();
			}
		}

	}
}
