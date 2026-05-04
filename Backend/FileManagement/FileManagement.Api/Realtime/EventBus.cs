using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace FileManagement.Api.Realtime
{
    public class EventBus
    {
        private readonly ConcurrentDictionary<Guid, Channel<string>> _subscribers = new();

        public ChannelReader<string> Subscribe(out Guid subscriptionId)
        {
            subscriptionId = Guid.NewGuid();
            var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            _subscribers[subscriptionId] = channel;
            return channel.Reader;
        }

        public void Unsubscribe(Guid subscriptionId)
        {
            if (_subscribers.TryRemove(subscriptionId, out var ch))
                ch.Writer.TryComplete();
        }

        public void Publish(object evt)
        {
            var json = JsonSerializer.Serialize(evt);
            foreach (var kv in _subscribers)
                kv.Value.Writer.TryWrite(json);
        }
    }
}

