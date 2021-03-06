using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AlternateLife.RageMP.Net.Data;
using AlternateLife.RageMP.Net.Native;
using AlternateLife.RageMP.Net.Scripting;

namespace AlternateLife.RageMP.Net.Helpers
{
    internal class RemoteEventHandler
    {
        private readonly Plugin _plugin;

        private readonly ConcurrentDictionary<string, HashSet<PlayerRemoteEventDelegate>> _subscriptions = new ConcurrentDictionary<string, HashSet<PlayerRemoteEventDelegate>>();

        internal RemoteEventHandler(Plugin plugin)
        {
            _plugin = plugin;
        }

        public void Subscribe(string eventName, PlayerRemoteEventDelegate callback)
        {
            if (_subscriptions.TryGetValue(eventName, out var eventSubscriptions) == false)
            {
                eventSubscriptions = new HashSet<PlayerRemoteEventDelegate>();

                if (_subscriptions.TryAdd(eventName, eventSubscriptions) == false)
                {
                    _plugin.Logger.Error($"Unable to create subscription list for event {eventName}");

                    return;
                }

                NativePlayerRemoteEventDelegate data = (playerPointer, arguments) => OnPlayerRemoteEvent(playerPointer, eventName, arguments);

                GCHandle.Alloc(data);

                using (var converter = new StringConverter())
                {
                    Rage.Multiplayer.Multiplayer_AddRemoteEventHandler(_plugin.NativeMultiplayer, converter.StringToPointer(eventName), data);
                }
            }

            eventSubscriptions.Add(callback);
        }

        private void OnPlayerRemoteEvent(IntPtr playerPointer, string eventName, ArgumentsData data)
        {
            if (_subscriptions.TryGetValue(eventName, out var eventSubscriptions) == false)
            {
                _plugin.Logger.Warn($"Unregistered remote event '{eventName}'");

                return;
            }

            var player = _plugin.PlayerPool[playerPointer];
            var arguments = _plugin.ArgumentConverter.ConvertToObjects(data);

            foreach (var subscription in eventSubscriptions)
            {
                try
                {
                    subscription(player, eventName, arguments);
                }
                catch (Exception e)
                {
                    _plugin.Logger.Error($"An error occured during execution of {eventName}", e);
                }
            }
        }
    }
}
