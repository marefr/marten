using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Baseline;
using Marten.Linq;
using Marten.Schema;
using Marten.Services;

namespace Marten.Events
{
    public class UnknownEventTypeException : Exception
    {
        public UnknownEventTypeException(string eventTypeName) : base($"Unknown event type name alias '{eventTypeName}.' You may need to register this event type through StoreOptions.Events.AddEventType(type)")
        {
        }
    }

    internal class EventSelector : ISelector<IEvent>
    {
        private readonly EventGraph _events;
        private readonly ISerializer _serializer;

        internal EventSelector(EventGraph events, ISerializer serializer)
        {
            _events = events;
            _serializer = serializer;
        }

        public IEvent Resolve(DbDataReader reader, IIdentityMap map)
        {
            var id = reader.GetGuid(0);
            var eventTypeName = reader.GetString(1);
            var version = reader.GetInt32(2);
            var dataJson = reader.GetString(3);


            var mapping = _events.EventMappingFor(eventTypeName);

            if (mapping == null)
            {
                throw new UnknownEventTypeException(eventTypeName);
            }

            var data = _serializer.FromJson(mapping.DocumentType, dataJson).As<object>();

            var sequence = reader.GetFieldValue<long>(4);
            var stream = reader.GetFieldValue<Guid>(5);

            var @event = EventStream.ToEvent(data);
            @event.Version = version;
            @event.Id = id;
            @event.Sequence = sequence;
            @event.StreamId = stream;

            return @event;
        }

        public async Task<IEvent> ResolveAsync(DbDataReader reader, IIdentityMap map, CancellationToken token)
        {
            var id = await reader.GetFieldValueAsync<Guid>(0, token).ConfigureAwait(false);
            var eventTypeName = await reader.GetFieldValueAsync<string>(1, token).ConfigureAwait(false);
            var version = await reader.GetFieldValueAsync<int>(2, token).ConfigureAwait(false);
            var dataJson = await reader.GetFieldValueAsync<string>(3, token).ConfigureAwait(false);

            var mapping = _events.EventMappingFor(eventTypeName);

            if (mapping == null)
            {
                throw new UnknownEventTypeException(eventTypeName);
            }

            var data = _serializer.FromJson(mapping.DocumentType, dataJson).As<object>();

            var sequence = await reader.GetFieldValueAsync<long>(4, token).ConfigureAwait(false);
            var stream = await reader.GetFieldValueAsync<Guid>(5, token).ConfigureAwait(false);

            var @event = EventStream.ToEvent(data);
            @event.Version = version;
            @event.Id = id;
            @event.Sequence = sequence;
            @event.StreamId = stream;

            return @event;
        }

        public string[] SelectFields()
        {
            return new[] {"id", "type", "version", "data", "seq_id", "stream_id"};
        }

        public string ToSelectClause(IQueryableDocument mapping)
        {
            return $"select id, type, version, data, seq_id, stream_id from {_events.DatabaseSchemaName}.mt_events";
        }
    }
}